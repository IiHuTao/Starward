using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;


namespace Starward.Features.Setting;

/// <summary>
/// 查看和管理 WebDAV 服务器上的备份文件
/// </summary>
[INotifyPropertyChanged]
public sealed partial class WebDAVBackupWindow : WindowEx
{


    private readonly ILogger<WebDAVBackupWindow> _logger = AppConfig.GetLogger<WebDAVBackupWindow>();


    public WebDAVBackupWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
    }


    private void InitializeWindow()
    {
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        Title = Lang.ToolboxSetting_WebDAVBackupManager;
        RootGrid.RequestedTheme = ShouldAppsUseDarkMode() ? ElementTheme.Dark : ElementTheme.Light;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
    }


    private async void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }


    private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= RootGrid_Loaded;
        RootGrid.Unloaded -= RootGrid_Unloaded;
        BackupFiles = null!;
    }


    public ObservableCollection<WebDAVFileInfo> BackupFiles { get; set => SetProperty(ref field, value); } = new();


    private readonly HashSet<WebDAVFileInfo> _selectedFiles = new();


    private bool _isOperating;


    public bool IsLoading { get; set => SetProperty(ref field, value); }


    public string? EmptyHint
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(EmptyHintVisibility));
            }
        }
    }


    public Visibility EmptyHintVisibility => string.IsNullOrEmpty(EmptyHint) ? Visibility.Collapsed : Visibility.Visible;


    public string? StatusText
    {
        get => field;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(StatusTextVisibility));
            }
        }
    }


    public Visibility StatusTextVisibility => string.IsNullOrEmpty(StatusText) ? Visibility.Collapsed : Visibility.Visible;


    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            if (!WebDAVClient.IsConfigured)
            {
                EmptyHint = Lang.WebDAVBackupWindow_NotConfigured;
                return;
            }
            IsLoading = true;
            StatusText = Lang.WebDAVBackupWindow_Loading;
            var files = await Task.Run(() => WebDAVClient.ListFilesAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password));
            BackupFiles.Clear();
            foreach (var file in files.OrderByDescending(x => x.LastModified))
            {
                BackupFiles.Add(file);
            }
            _selectedFiles.Clear();
            UpdateButtonState();
            EmptyHint = files.Count == 0 ? Lang.WebDAVBackupWindow_Empty : null;
            StatusText = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "List WebDAV backup files");
            StatusText = null;
            InAppToast.MainWindow?.Error(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }


    [RelayCommand]
    private async Task DownloadAsync()
    {
        _isOperating = true;
        UpdateButtonState();
        try
        {
            if (_selectedFiles.Count > 0)
            {
                string? folder = await FileDialogHelper.PickFolderAsync(this.Content.XamlRoot);
                if (Directory.Exists(folder))
                {
                    foreach (var file in _selectedFiles.ToList())
                    {
                        string localPath = Path.Combine(folder, Path.GetFileName(file.Name));
                        StatusText = $"{Lang.WebDAVBackupWindow_Downloading}  {file.Name}";
                        await Task.Run(async () =>
                        {
                            var stopwatch = Stopwatch.StartNew();
                            var progress = WebDAVClient.CreateThrottledProgress(file.Size, stopwatch, (percent, speed) =>
                            {
                                string text = $"{Lang.WebDAVBackupWindow_Downloading}  {file.Name} · {file.SizeText} · {percent:F0}% · {WebDAVClient.FormatSpeed(speed)}";
                                DispatcherQueue.TryEnqueue(() => StatusText = text);
                            });
                            await WebDAVClient.DownloadFileAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password, file.Url, localPath, progress);
                        });
                    }
                    StatusText = null;
                    await Launcher.LaunchFolderPathAsync(folder);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download WebDAV backup file");
            StatusText = null;
            InAppToast.MainWindow?.Error(ex);
        }
        finally
        {
            _isOperating = false;
            UpdateButtonState();
        }
    }


    [RelayCommand]
    private async Task DeleteAsync()
    {
        _isOperating = true;
        UpdateButtonState();
        try
        {
            if (_selectedFiles.Count > 0)
            {
                var dialog = new ContentDialog
                {
                    Title = Lang.WebDAVBackupWindow_DeleteConfirmTitle,
                    Content = $"{Lang.WebDAVBackupWindow_DeleteConfirmContent}\n{string.Join("\n", _selectedFiles.Select(x => x.Name))}",
                    PrimaryButtonText = Lang.Common_Confirm,
                    SecondaryButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = this.Content.XamlRoot,
                };
                if (await dialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    StatusText = Lang.WebDAVBackupWindow_Deleting;
                    foreach (var file in _selectedFiles.ToList())
                    {
                        await WebDAVClient.DeleteFileAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password, file.Url);
                    }
                    StatusText = null;
                    await RefreshAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete WebDAV backup file");
            StatusText = null;
            InAppToast.MainWindow?.Error(ex);
        }
        finally
        {
            _isOperating = false;
            UpdateButtonState();
        }
    }


    private void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: WebDAVFileInfo file })
        {
            _selectedFiles.Add(file);
            UpdateButtonState();
        }
    }


    private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: WebDAVFileInfo file })
        {
            _selectedFiles.Remove(file);
            UpdateButtonState();
        }
    }


    private void UpdateButtonState()
    {
        bool hasSelection = _selectedFiles.Count > 0 && !_isOperating;
        Button_Download.IsEnabled = hasSelection;
        Button_Delete.IsEnabled = hasSelection;
        Button_Refresh.IsEnabled = !_isOperating;
    }


}
