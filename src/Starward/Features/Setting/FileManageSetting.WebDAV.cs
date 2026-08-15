using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using SharpSevenZip;
using Starward.Features.Database;
using Starward.Helpers;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;


namespace Starward.Features.Setting;

public sealed partial class FileManageSetting
{




    #region WebDAV备份数据库



    public string? LastWebDAVBackupTime { get; set => SetProperty(ref field, value); }

    public bool IsLastWebDAVBackupExists { get; set => SetProperty(ref field, value); }

    public string? WebDAVUploadProgress { get; set => SetProperty(ref field, value); }


    private (string? FileName, string? LocalFile, DateTime Time, bool LocalExists, bool WebDAVExists)? _lastWebDAVBackupState;

    private DateTime _lastWebDAVBackupStateTime;


    /// <summary>
    /// 读取上次 WebDAV 备份记录，返回本地文件路径、备份时间与本地/服务器上的存在状态（短时缓存，避免重复请求服务器）
    /// </summary>
    private async Task<(string? FileName, string? LocalFile, DateTime Time, bool LocalExists, bool WebDAVExists)> GetLastWebDAVBackupStateAsync()
    {
        var cached = _lastWebDAVBackupState;
        if (cached is not null && DateTime.Now - _lastWebDAVBackupStateTime < TimeSpan.FromSeconds(30))
        {
            return cached.Value;
        }
        var state = await GetLastWebDAVBackupStateCoreAsync();
        _lastWebDAVBackupState = state;
        _lastWebDAVBackupStateTime = DateTime.Now;
        return state;
    }


    private async Task<(string? FileName, string? LocalFile, DateTime Time, bool LocalExists, bool WebDAVExists)> GetLastWebDAVBackupStateCoreAsync()
    {
        if (!DatabaseService.TryGetValue("LastWebDAVBackupDatabase", out string? file, out DateTime time))
        {
            return (null, null, default, false, false);
        }
        string fileName = Path.GetFileName(file!);
        string localFile = Path.Join(AppConfig.UserDataFolder, "DatabaseBackup", fileName);
        bool localExists = File.Exists(localFile);
        bool webdavExists = false;
        if (!string.IsNullOrWhiteSpace(WebDAVClient.ServerAddress))
        {
            string fileUrl = WebDAVClient.GetBackupFolderUrl(WebDAVClient.ServerAddress) + "/" + Uri.EscapeDataString(fileName);
            try
            {
                webdavExists = await Task.Run(() => WebDAVClient.CheckFileExistsAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password, fileUrl));
            }
            catch (Exception ex)
            {
                // 服务器不可达时按仅本地存在处理
                _logger.LogWarning(ex, "Check WebDAV backup file existence");
            }
        }
        return (fileName, localFile, time, localExists, webdavExists);
    }


    private async Task GetLastWebDAVBackupTimeAsync()
    {
        try
        {
            var (_, _, time, localExists, webdavExists) = await GetLastWebDAVBackupStateAsync();
            if (localExists || webdavExists)
            {
                LastWebDAVBackupTime = $"{Lang.SettingPage_LastBackup}  {time:yyyy-MM-dd HH:mm:ss}";
                IsLastWebDAVBackupExists = localExists || webdavExists;
            }
            else
            {
                LastWebDAVBackupTime = null;
                IsLastWebDAVBackupExists = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Get last WebDAV backup time");
            // 服务器不可达等检测失败时，仅按本地文件是否存在决定是否显示
            if (DatabaseService.TryGetValue("LastWebDAVBackupDatabase", out string? file, out DateTime time) && File.Exists(Path.Join(AppConfig.UserDataFolder, "DatabaseBackup", Path.GetFileName(file))))
            {
                LastWebDAVBackupTime = $"{Lang.SettingPage_LastBackup}  {time:yyyy-MM-dd HH:mm:ss}";
                IsLastWebDAVBackupExists = true;
            }
            else
            {
                LastWebDAVBackupTime = null;
                IsLastWebDAVBackupExists = false;
            }
        }
    }


    [RelayCommand]
    private async Task OpenLastWebDAVBackupDatabaseAsync()
    {
        try
        {
            var (_, localFile, _, localExists, webdavExists) = await GetLastWebDAVBackupStateAsync();
            if (localFile is null)
            {
                return;
            }
            if (localExists && !webdavExists)
            {
                // 仅本地存在：打开本地文件所在文件夹
                var item = await StorageFile.GetFileFromPathAsync(localFile);
                var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(localFile)!);
                var options = new FolderLauncherOptions
                {
                    ItemsToSelect = { item }
                };
                await Launcher.LaunchFolderAsync(folder, options);
            }
            else
            {
                // 本地与 WebDAV 均存在，或仅 WebDAV 存在：打开 WebDAV 备份管理
                new WebDAVBackupWindow().Activate();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open last WebDAV backup database");
            InAppToast.MainWindow?.Error(ex);
        }
    }





    /// <summary>
    /// 打开 WebDAV 配置
    /// </summary>
    [RelayCommand]
    private async Task OpenWebDAVSettingAsync()
    {
        try
        {
            var textBoxServerAddress = new TextBox
            {
                Text = AppConfig.GetValue<string>(null, "WebDAVServerAddress") ?? "",
                Header = Lang.SettingPage_WebDAVServerAddress,
                PlaceholderText = "https://example.com/dav/",
            };
            var textBoxUserName = new TextBox
            {
                Text = AppConfig.GetValue<string>(null, "WebDAVUserName") ?? "",
                Header = Lang.SettingPage_WebDAVUserName,
            };
            var passwordBox = new PasswordBox
            {
                Password = AppConfig.GetValue<string>(null, "WebDAVPassword") ?? "",
                Header = Lang.SettingPage_WebDAVPassword,
                PasswordRevealMode = PasswordRevealMode.Hidden,
            };
            var fontIconReveal = new FontIcon { FontSize = 14, Glyph = "\uE7B3", Visibility = Visibility.Collapsed };
            var fontIconHide = new FontIcon { FontSize = 14, Glyph = "\uED1A" };
            var toggleRevealButton = new ToggleButton
            {
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Bottom,
                Content = new Grid
                {
                    Children =
                    {
                        fontIconReveal,
                        fontIconHide,
                    }
                },
            };
            toggleRevealButton.Checked += (_, _) =>
            {
                passwordBox.PasswordRevealMode = PasswordRevealMode.Visible;
                fontIconReveal.Visibility = Visibility.Visible;
                fontIconHide.Visibility = Visibility.Collapsed;
            };
            toggleRevealButton.Unchecked += (_, _) =>
            {
                passwordBox.PasswordRevealMode = PasswordRevealMode.Hidden;
                fontIconReveal.Visibility = Visibility.Collapsed;
                fontIconHide.Visibility = Visibility.Visible;
            };
            var passwordRow = new Grid { ColumnSpacing = 8 };
            passwordRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            passwordRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(passwordBox, 0);
            Grid.SetColumn(toggleRevealButton, 1);
            passwordRow.Children.Add(passwordBox);
            passwordRow.Children.Add(toggleRevealButton);
            var errorText = new TextBlock
            {
                Foreground = App.Current.Resources["SystemFillColorCriticalBrush"] as Brush,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            var testProgressRing = new ProgressRing
            {
                Width = 16,
                Height = 16,
                IsIndeterminate = false,
                Visibility = Visibility.Collapsed,
            };
            var testConnectionButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        testProgressRing,
                        new TextBlock { Text = Lang.SettingPage_WebDAVTestConnection },
                    }
                },
            };
            var testResultText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = App.Current.Resources["SystemFillColorSuccessBrush"] as Brush,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed,
            };
            var connectionTestRow = new Grid { ColumnSpacing = 8 };
            connectionTestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            connectionTestRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(testConnectionButton, 0);
            Grid.SetColumn(testResultText, 1);
            connectionTestRow.Children.Add(testConnectionButton);
            connectionTestRow.Children.Add(testResultText);
            testConnectionButton.Click += async (_, _) =>
            {
                testConnectionButton.IsEnabled = false;
                try
                {
                    errorText.Visibility = Visibility.Collapsed;
                    testResultText.Visibility = Visibility.Collapsed;
                    string testServerAddress = textBoxServerAddress.Text.Trim();
                    if (testServerAddress.Length == 0 || !Uri.TryCreate(testServerAddress.TrimEnd('/') + "/", UriKind.Absolute, out var testUri) || testUri.Scheme is not ("http" or "https"))
                    {
                        errorText.Text = Lang.SettingPage_WebDAVInvalidServerAddress;
                        errorText.Visibility = Visibility.Visible;
                        return;
                    }
                    testServerAddress = testServerAddress.TrimEnd('/') + "/";
                    string testUserName = textBoxUserName.Text.Trim();
                    string testPassword = passwordBox.Password;
                    testProgressRing.Visibility = Visibility.Visible;
                    testProgressRing.IsIndeterminate = true;
                    await Task.Run(() => WebDAVClient.CheckFileExistsAsync(testServerAddress, testUserName, testPassword, WebDAVClient.GetBackupFolderUrl(testServerAddress)));
                    testResultText.Foreground = App.Current.Resources["SystemFillColorSuccessBrush"] as Brush;
                    testResultText.Text = Lang.SettingPage_WebDAVTestSuccess;
                    testResultText.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Test WebDAV connection");
                    testResultText.Foreground = App.Current.Resources["SystemFillColorCriticalBrush"] as Brush;
                    testResultText.Text = ex.Message;
                    testResultText.Visibility = Visibility.Visible;
                }
                finally
                {
                    testProgressRing.IsIndeterminate = false;
                    testProgressRing.Visibility = Visibility.Collapsed;
                    testConnectionButton.IsEnabled = true;
                }
            };
            var checkBoxDeleteLocalFile = new CheckBox
            {
                Content = Lang.SettingPage_WebDAVDeleteLocalFileAfterUpload,
                IsChecked = AppConfig.GetValue<bool>(false, "WebDAVDeleteLocalFileAfterUpload"),
            };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(textBoxServerAddress);
            panel.Children.Add(errorText);
            panel.Children.Add(textBoxUserName);
            panel.Children.Add(passwordRow);
            panel.Children.Add(connectionTestRow);
            panel.Children.Add(checkBoxDeleteLocalFile);
            while (true)
            {
                testResultText.Visibility = Visibility.Collapsed;
                var dialog = new ContentDialog
                {
                    Title = Lang.SettingPage_WebDAVConfig,
                    Content = panel,
                    PrimaryButtonText = Lang.Common_Confirm,
                    SecondaryButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot,
                    Resources =
                    {
                        ["ContentDialogMaxWidth"] = 370.0,
                        ["ContentDialogMinWidth"] = 370.0,
                    },
                };
                if (await dialog.ShowAsync() is not ContentDialogResult.Primary)
                {
                    return;
                }
                string serverAddressValue = textBoxServerAddress.Text.Trim();
                if (serverAddressValue.Length > 0)
                {
                    serverAddressValue = serverAddressValue.TrimEnd('/') + "/";
                    if (!Uri.TryCreate(serverAddressValue, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                    {
                        errorText.Text = Lang.SettingPage_WebDAVInvalidServerAddress;
                        errorText.Visibility = Visibility.Visible;
                        continue;
                    }
                }
                AppConfig.SetValue(serverAddressValue, "WebDAVServerAddress");
                AppConfig.SetValue(textBoxUserName.Text.Trim(), "WebDAVUserName");
                AppConfig.SetValue(passwordBox.Password, "WebDAVPassword");
                AppConfig.SetValue(checkBoxDeleteLocalFile.IsChecked == true, "WebDAVDeleteLocalFileAfterUpload");
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open WebDAV setting");
        }
    }


    /// <summary>
    /// WebDAV 备份数据库
    /// </summary>
    [RelayCommand]
    private async Task WebDAVBackupDatabaseAsync()
    {
        string? archive = null;
        try
        {
            if (!WebDAVClient.IsConfigured)
            {
                await OpenWebDAVSettingAsync();
                if (!WebDAVClient.IsConfigured)
                {
                    return;
                }
            }
            string serverAddress = WebDAVClient.ServerAddress;
            string? userName = WebDAVClient.UserName;
            string? password = WebDAVClient.Password;
            if (Directory.Exists(AppConfig.UserDataFolder))
            {
                var folder = Path.Combine(AppConfig.UserDataFolder, "DatabaseBackup");
                Directory.CreateDirectory(folder);
                DateTime time = DateTime.Now;
                WebDAVUploadProgress = Lang.SettingPage_Compressing;
                await Task.Run(async () =>
                {
                    string file = Path.Combine(folder, $"StarwardDatabase_WebDAV_{time:yyyyMMdd_HHmmss}.db");
                    archive = Path.ChangeExtension(file, ".7z");
                    DatabaseService.BackupDatabase(file);
                    new SharpSevenZipCompressor().CompressFiles(archive, file);
                    File.Delete(file);
                    long totalBytes = new FileInfo(archive).Length;
                    var stopwatch = Stopwatch.StartNew();
                    var progress = WebDAVClient.CreateThrottledProgress(totalBytes, stopwatch, (percent, speed) =>
                    {
                        string text = $"{WebDAVClient.FormatFileSize(totalBytes)} · {percent:F0}% · {WebDAVClient.FormatSpeed(speed)}";
                        DispatcherQueue.TryEnqueue(() => WebDAVUploadProgress = text);
                    });
                    await WebDAVClient.UploadFileAsync(serverAddress, userName, password, archive, progress);
                    DatabaseService.SetValue("LastWebDAVBackupDatabase", Path.GetFileName(archive), time);
                    _lastWebDAVBackupState = null;
                    if (AppConfig.GetValue<bool>(false, "WebDAVDeleteLocalFileAfterUpload"))
                    {
                        try
                        {
                            File.Delete(archive);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Delete local WebDAV backup archive");
                        }
                    }
                });
                WebDAVUploadProgress = null;
                IsLastWebDAVBackupExists = true;
                LastWebDAVBackupTime = $"{Lang.SettingPage_LastBackup}  {time:yyyy-MM-dd HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebDAV backup database");
            InAppToast.MainWindow?.Error(ex);
            WebDAVUploadProgress = null;
            if (archive is not null && File.Exists(archive))
            {
                await ConfirmDeleteLocalArchiveAsync(archive);
            }
        }
    }


    private async Task ConfirmDeleteLocalArchiveAsync(string archive)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = Lang.Common_Warning,
                Content = Lang.SettingPage_WebDAVBackupFailedCleanup,
                PrimaryButtonText = Lang.Common_Delete,
                SecondaryButtonText = Lang.Common_Cancel,
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = this.XamlRoot,
            };
            if (await dialog.ShowAsync() is ContentDialogResult.Primary)
            {
                File.Delete(archive);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Delete local WebDAV backup archive after failed upload");
        }
    }



    #endregion




}
