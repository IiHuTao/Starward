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
    }


    public ObservableCollection<WebDAVFileInfo> BackupFiles { get; set => SetProperty(ref field, value); } = new();


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
                UpdateButtonState();
                return;
            }
            if (IsLoading)
            {
                return;
            }
            IsLoading = true;
            UpdateButtonState();
            StatusText = Lang.WebDAVBackupWindow_Loading;
            var files = await Task.Run(() => WebDAVClient.ListFilesAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password));
            BackupFiles.Clear();
            foreach (var file in files.OrderByDescending(x => x.LastModified))
            {
                BackupFiles.Add(file);
            }
            ListView_Files.SelectedItems.Clear();
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
            UpdateButtonState();
        }
    }


    private bool _isUpdatingSelectAll;


    private void CheckBox_SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isUpdatingSelectAll)
        {
            ListView_Files.SelectAll();
        }
    }


    private void CheckBox_SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        if (!_isUpdatingSelectAll)
        {
            ListView_Files.SelectedItems.Clear();
        }
    }


    private void UpdateSelectAllCheckBox()
    {
        _isUpdatingSelectAll = true;
        CheckBox_SelectAll.IsChecked = BackupFiles.Count > 0 && ListView_Files.SelectedItems.Count == BackupFiles.Count;
        _isUpdatingSelectAll = false;
    }


    [RelayCommand]
    private async Task DeleteBeforeDateAsync()
    {
        _isOperating = true;
        UpdateButtonState();
        try
        {
            if (BackupFiles.Count > 0)
            {
                var hint = new TextBlock
                {
                    Text = Lang.WebDAVBackupWindow_DeleteBeforeDateContent,
                    Foreground = App.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
                    TextWrapping = TextWrapping.Wrap,
                };
                var dateLabel = new TextBlock
                {
                    Text = Lang.WebDAVBackupWindow_Date,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var datePicker = new CalendarDatePicker
                {
                    DateFormat = "{year.full}/{month.integer}/{day.integer}",
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var countText = new TextBlock
                {
                    Foreground = App.Current.Resources["TextFillColorSecondaryBrush"] as Brush,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                datePicker.DateChanged += (_, _) =>
                {
                    int count = datePicker.Date is DateTimeOffset date
                        ? BackupFiles.Count(x => x.LastModified != DateTime.MinValue && x.LastModified < date.UtcDateTime)
                        : 0;
                    countText.Text = count > 0 ? string.Format(Lang.WebDAVBackupWindow_DeleteBeforeDateCount, count) : "";
                };
                var dateRow = new Grid { ColumnSpacing = 8 };
                dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                dateRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                Grid.SetColumn(datePicker, 1);
                Grid.SetColumn(countText, 2);
                dateRow.Children.Add(dateLabel);
                dateRow.Children.Add(datePicker);
                dateRow.Children.Add(countText);
                var panel = new StackPanel { Spacing = 12 };
                panel.Children.Add(hint);
                panel.Children.Add(dateRow);
                var dialog = new ContentDialog
                {
                    Title = Lang.WebDAVBackupWindow_DeleteBeforeDateTitle,
                    Content = panel,
                    PrimaryButtonText = Lang.Common_Delete,
                    SecondaryButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = this.Content.XamlRoot,
                };
                if (await dialog.ShowAsync() is ContentDialogResult.Primary && datePicker.Date is DateTimeOffset date)
                {
                    // LastModified 解析失败（MinValue）的文件不参与删除
                    var targets = BackupFiles.Where(x => x.LastModified != DateTime.MinValue && x.LastModified < date.UtcDateTime).ToList();
                    if (targets.Count > 0)
                    {
                        StatusText = Lang.WebDAVBackupWindow_Deleting;
                        foreach (var file in targets)
                        {
                            await WebDAVClient.DeleteFileAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password, file.Url);
                        }
                        StatusText = null;
                        await RefreshAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete WebDAV backup files before date");
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
    private async Task DownloadAsync()
    {
        _isOperating = true;
        UpdateButtonState();
        try
        {
            if (ListView_Files.SelectedItems.Count > 0)
            {
                string? folder = await FileDialogHelper.PickFolderAsync(this.Content.XamlRoot);
                if (Directory.Exists(folder))
                {
                    foreach (var file in ListView_Files.SelectedItems.OfType<WebDAVFileInfo>().ToList())
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
                            await WebDAVClient.DownloadFileAsync(WebDAVClient.ServerAddress, WebDAVClient.UserName, WebDAVClient.Password, file.Url, localPath, progress, file.Size);
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
            if (ListView_Files.SelectedItems.Count > 0)
            {
                var dialog = new ContentDialog
                {
                    Title = Lang.WebDAVBackupWindow_DeleteConfirmTitle,
                    Content = Lang.WebDAVBackupWindow_DeleteConfirmContent,
                    PrimaryButtonText = Lang.Common_Confirm,
                    SecondaryButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = this.Content.XamlRoot,
                };
                if (await dialog.ShowAsync() is ContentDialogResult.Primary)
                {
                    StatusText = Lang.WebDAVBackupWindow_Deleting;
                    foreach (var file in ListView_Files.SelectedItems.OfType<WebDAVFileInfo>().ToList())
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


    private void ListView_Files_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateButtonState();
    }


    private void UpdateButtonState()
    {
        bool hasSelection = ListView_Files.SelectedItems.Count > 0 && !_isOperating;
        Button_Download.IsEnabled = hasSelection;
        Button_Delete.IsEnabled = hasSelection;
        CheckBox_SelectAll.IsEnabled = BackupFiles.Count > 0 && !_isOperating;
        Button_DeleteBeforeDate.IsEnabled = BackupFiles.Count > 0 && !_isOperating;
        Button_Refresh.IsEnabled = !_isOperating && !IsLoading;
        UpdateSelectAllCheckBox();
        UpdateSelectionStatus();
    }


    private void UpdateSelectionStatus()
    {
        if (_isOperating || IsLoading)
        {
            return;
        }
        StatusText = ListView_Files.SelectedItems.Count > 0
            ? string.Format(Lang.WebDAVBackupWindow_SelectedCount, ListView_Files.SelectedItems.Count)
            : null;
    }


}
