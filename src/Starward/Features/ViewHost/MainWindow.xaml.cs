using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Starward.Features.Background;
using Starward.Features.Database;
using Starward.Features.GameLauncher;
using Starward.Features.GamepadControl;
using Starward.Features.Overlay;
using Starward.Features.Screenshot;
using Starward.Frameworks;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.Graphics;


namespace Starward.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class MainWindow : WindowEx
{


    public static new MainWindow Current { get; private set; }


    private bool _mainViewLoaded;


    public MainWindow()
    {
        Current = this;
        MainWindowId = AppWindow.Id;
        this.InitializeComponent();
        InitializeMainWindow();
        LoadContentView();
        WeakReferenceMessenger.Default.Register<AccentColorChangedMessage>(this, OnAccentColorChanged);
        WeakReferenceMessenger.Default.Register<WelcomePageFinishedMessage>(this, OnWelcomePageFinished);
        WeakReferenceMessenger.Default.Register<GameStartedMessage>(this, OnGameStarted);
    }



    private void InitializeMainWindow()
    {
        Title = "Starward";
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Closing += AppWindow_Closing;
        Content.KeyDown += Content_KeyDown;
        CenterInScreen(1200, 676);
        AdaptTitleBarButtonColorToActuallTheme();
        SetDragRectangles(new RectInt32(0, 0, 100000, (int)(48 * UIScale)));
        SetIcon();
        WTSRegisterSessionNotification(WindowHandle, 0);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }



    public override void CenterInScreen(int? width = null, int? height = null)
    {
        width = width <= 0 ? null : width;
        height = height <= 0 ? null : height;
        User32.GetCursorPos(out POINT point);
        DisplayArea display = DisplayArea.GetFromPoint(new PointInt32(point.X, point.Y), DisplayAreaFallback.Nearest);
        double scale = UIScale;
        int w = (int)((width * scale) ?? AppWindow.Size.Width);
        int h = (int)((height * scale) ?? AppWindow.Size.Height);
        int x = display.WorkArea.X + (display.WorkArea.Width - w) / 2;
        int y = display.WorkArea.Y + (display.WorkArea.Height - h) / 2;
        AppWindow.MoveAndResize(new RectInt32(x, y, w, h));
    }



    public override void Show()
    {
        double uiScale = UIScale;
        if (Math.Abs(AppWindow.Size.Width - 1200 * uiScale) > 10 || Math.Abs(AppWindow.Size.Height - 676 * uiScale) > 10)
        {
            CenterInScreen(1200, 676);
        }
        base.Show();
    }



    public void ShowByGamepad()
    {
        CenterInScreen(1200, 676);
        User32.SetCursorPos(AppWindow.Position.X + AppWindow.Size.Width / 2, AppWindow.Position.Y + AppWindow.Size.Height / 2);
        base.Show();
    }



    private void LoadContentView()
    {
        if (string.IsNullOrWhiteSpace(AppConfig.UserDataFolder))
        {
            MainContentHost.Content = new WelcomeView();
        }
        else
        {
            MainContentHost.Content = new MainView();
            App.Current.EnsureSystemTray();
            _mainViewLoaded = true;
        }
    }



    private void OnWelcomePageFinished(object _, WelcomePageFinishedMessage __)
    {
        MainContentHost.Content = new MainView();
        App.Current.EnsureSystemTray();
        _mainViewLoaded = true;
    }



    private void OnGameStarted(object _, GameStartedMessage __)
    {
        if (_mainViewLoaded)
        {
            StartGameAction action = AppConfig.StartGameAction;
            if (action is StartGameAction.Hide)
            {
                this.Hide();
            }
            else if (action is StartGameAction.Minimize)
            {
                this.Minimize();
            }
        }
    }



    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        try
        {
            if (!_mainViewLoaded)
            {
                App.Current.Exit();
                return;
            }
            args.Cancel = true;
            MainWindowCloseOption option = AppConfig.CloseWindowOption;
            if (option is not MainWindowCloseOption.Hide and not MainWindowCloseOption.Exit)
            {
                var dialog = new MainWindowCloseDialog
                {
                    Title = Lang.ExperienceSettingPage_CloseWindowOption,
                    PrimaryButtonText = Lang.Common_Confirm,
                    CloseButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot,
                };
                var result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary)
                {
                    return;
                }
                option = dialog.MainWindowCloseOption.Value;
                AppConfig.CloseWindowOption = option;
            }
            if (option is MainWindowCloseOption.Hide)
            {
                Hide();
            }
            if (option is MainWindowCloseOption.Exit)
            {
                Close();
                AppInstance.GetCurrent().UnregisterKey();
                GamepadController.RestoreGamepadGuideButtonForGameBar();
                Task backupTask = Task.Run(DatabaseService.AutoBackupToAppDataLocal);
                Task timeTask = Task.Delay(30000);
                await Task.WhenAny(backupTask, timeTask);
                App.Current.Exit();
            }
        }
        catch { }
    }



    private void OnAccentColorChanged(object _, AccentColorChangedMessage __)
    {
        FrameworkElement ele = (FrameworkElement)Content;
        ele.RequestedTheme = ele.ActualTheme switch
        {
            ElementTheme.Light => ElementTheme.Dark,
            ElementTheme.Dark => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
        ele.RequestedTheme = ElementTheme.Default;
    }



    private void Content_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_mainViewLoaded)
        {
            if (e.Key is Windows.System.VirtualKey.Escape)
            {
                Hide();
            }
        }
    }



    public override void Hide()
    {
        base.Hide();
        WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage { Hide = true, CurrentTime = DateTimeOffset.Now });
        GC.Collect();
    }



    private DateTimeOffset _lastActivatedTime = DateTimeOffset.Now;


    protected override nint WindowSubclassProc(HWND hWnd, uint uMsg, nint wParam, nint lParam, nuint uIdSubclass, nint dwRefData)
    {
        if (uMsg == (uint)User32.WindowMessage.WM_ACTIVATE || uMsg == (uint)User32.WindowMessage.WM_POINTERACTIVATE)
        {
            // 窗口激活
            if (wParam is 0x1 or 0x2)
            {
                // WA_ACTIVE or WA_CLICKACTIVE
                var now = DateTimeOffset.Now;
                WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage
                {
                    Activate = true,
                    CurrentTime = now,
                    LastActivatedTime = _lastActivatedTime,
                });
                _lastActivatedTime = now;
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_SYSCOMMAND)
        {
            if (wParam == 0xF030)
            {
                // SC_MAXIMIZE
                // 防止双击标题栏使窗口最大化，WinAppSDK 某个版本的 Bug
                return IntPtr.Zero;
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_WTSSESSION_CHANGE)
        {
            if (wParam == 0x7)
            {
                // WTS_SESSION_LOCK
                // 锁屏，暂停视频背景
                WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage { SessionLock = true, CurrentTime = DateTimeOffset.Now });
            }
            else if (wParam == 0x8)
            {
                // WTS_SESSION_UNLOCK 
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_DEVICECHANGE)
        {
            // 存储设备插入/拔出
            if (wParam == 0x8000)
            {
                // DBT_DEVICEARRIVAL
                User32.DEV_BROADCAST_HDR dev = Marshal.PtrToStructure<User32.DEV_BROADCAST_HDR>(lParam);
                if (dev.dbch_devicetype is User32.DBT_DEVTYPE.DBT_DEVTYP_VOLUME)
                {
                    WeakReferenceMessenger.Default.Send(new RemovableStorageDeviceChangedMessage());
                }
            }
            else if (wParam == 0x8004)
            {
                // DBT_DEVICEREMOVECOMPLETE
                User32.DEV_BROADCAST_HDR dev = Marshal.PtrToStructure<User32.DEV_BROADCAST_HDR>(lParam);
                if (dev.dbch_devicetype is User32.DBT_DEVTYPE.DBT_DEVTYP_VOLUME)
                {
                    WeakReferenceMessenger.Default.Send(new RemovableStorageDeviceChangedMessage());
                }
            }
        }
        else if (uMsg == (uint)User32.WindowMessage.WM_HOTKEY)
        {
            if (wParam == 44444)
            {
                if (!RunningGameService.OpenOverlayWindow())
                {
                    this.Show();
                }
            }
            else if (wParam == 44445)
            {
                // 截图
                ScreenCaptureService.Capture();
            }
        }
        return base.WindowSubclassProc(hWnd, uMsg, wParam, lParam, uIdSubclass, dwRefData);
    }



    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSRegisterSessionNotification(IntPtr hWnd, int dwFlags);


}
