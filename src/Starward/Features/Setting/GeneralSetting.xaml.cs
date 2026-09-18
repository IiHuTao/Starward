using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Features.ViewHost;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Globalization;
using Windows.System;


namespace Starward.Features.Setting;

public sealed partial class GeneralSetting : PageBase
{

    private readonly ILogger<GeneralSetting> _logger = AppConfig.GetLogger<GeneralSetting>();


    public GeneralSetting()
    {
        this.InitializeComponent();
    }



    protected override void OnLoaded()
    {
        InitializeLanguageSelector();
        InitializeCloseWindowOption();
        InitializeFontFamilySelector();
    }




    #region 语言



    private bool _languageInitialized;


    /// <summary>
    /// 语言
    /// </summary>
    private void InitializeLanguageSelector()
    {
        try
        {
            var lang = AppConfig.Language;
            ComboBox_Language.Items.Clear();
            ComboBox_Language.Items.Add(new ComboBoxItem
            {
                Content = Lang.ResourceManager.GetString(nameof(Lang.SettingPage_FollowSystem), CultureInfo.InstalledUICulture),
                Tag = "",
            });
            ComboBox_Language.SelectedIndex = 0;
            foreach (var (Title, LangCode) in Localization.LanguageList)
            {
                var box = new ComboBoxItem
                {
                    Content = Title,
                    Tag = LangCode,
                };
                ComboBox_Language.Items.Add(box);
                if (LangCode == lang)
                {
                    ComboBox_Language.SelectedItem = box;
                }
            }
        }
        finally
        {
            _languageInitialized = true;
        }
    }



    /// <summary>
    /// 语言切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ComboBox_Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (ComboBox_Language.SelectedItem is ComboBoxItem item)
            {
                if (_languageInitialized)
                {
                    var lang = item.Tag as string;
                    _logger.LogInformation("Language change to {lang}", lang);
                    AppConfig.SetLanguage(lang);
                    this.Bindings.Update();
                    InitializeFontFamilySelector();
                    WeakReferenceMessenger.Default.Send(new LanguageChangedMessage());
                    AppConfig.SaveConfiguration();
                }
            }
        }
        catch (CultureNotFoundException)
        {
            AppConfig.SetLanguage(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change Language");
        }
    }



    #endregion



    #region 关闭窗口选项



    private bool _closeWindowOptionInitialized;



    /// <summary>
    /// 初始化关闭窗口选项
    /// </summary>
    private void InitializeCloseWindowOption()
    {
        try
        {
            var option = AppConfig.CloseWindowOption;
            if (option is MainWindowCloseOption.Hide)
            {
                RadioButton_CloseWindowOption_Hide.IsChecked = true;
            }
            else if (option is MainWindowCloseOption.Exit)
            {
                RadioButton_CloseWindowOption_Exit.IsChecked = true;
            }
            _closeWindowOptionInitialized = true;
        }
        catch { }
    }



    /// <summary>
    /// 关闭窗口选项切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void RadioButton_CloseWindowOption_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_closeWindowOptionInitialized)
            {
                if (sender is FrameworkElement fe)
                {
                    AppConfig.CloseWindowOption = fe.Tag switch
                    {
                        MainWindowCloseOption option => option,
                        _ => 0,
                    };
                }
            }
        }
        catch { }
    }



    #endregion



    #region 游戏账号切换



    public bool EnableGameAccountSwitcher
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.EnableGameAccountSwitcher = value;
            }
        }
    } = AppConfig.EnableGameAccountSwitcher;



    #endregion



    #region 自定义字体



    private bool _fontFamilyInitialized;


    /// <summary>
    /// 初始化自定义字体选择器
    /// </summary>
    private void InitializeFontFamilySelector()
    {
        try
        {
            // 重建列表过程中的 SelectionChanged 不生效，避免字体被瞬时重置
            _fontFamilyInitialized = false;
            string? current = AppConfig.CustomFontFamily;
            ComboBox_FontFamily.Items.Clear();
            ComboBox_FontFamily.Items.Add(new ComboBoxItem
            {
                Content = Lang.SettingPage_DefaultFont,
            });
            ComboBox_FontFamily.SelectedIndex = 0;
            foreach (var font in FontHelper.GetSystemFontFamilies())
            {
                var box = new ComboBoxItem
                {
                    Content = font.LocalizedName,
                    // 保存 en-us 规范名称，不受应用界面语言影响
                    Tag = font.CanonicalName,
                };
                ComboBox_FontFamily.Items.Add(box);
                if (font.CanonicalName == current)
                {
                    ComboBox_FontFamily.SelectedItem = box;
                }
            }
            if (ComboBox_FontFamily.SelectedItem is null && !string.IsNullOrWhiteSpace(current))
            {
                // 已设置的字体不在列表中（可能已被卸载），附加显示以保持选中状态
                var box = new ComboBoxItem
                {
                    Content = current,
                    Tag = current,
                };
                ComboBox_FontFamily.Items.Add(box);
                ComboBox_FontFamily.SelectedItem = box;
            }
        }
        finally
        {
            _fontFamilyInitialized = true;
        }
    }


    /// <summary>
    /// 自定义字体切换
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ComboBox_FontFamily_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (_fontFamilyInitialized && ComboBox_FontFamily.SelectedItem is ComboBoxItem item)
            {
                string? font = item.Tag as string;
                _logger.LogInformation("Custom font family change to {font}", font);
                AppConfig.CustomFontFamily = font;
                FontHelper.ApplyCustomFont(font);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change Custom Font Family");
        }
    }



    /// <summary>
    /// 重置自定义字体
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_ResetFont_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ComboBox_FontFamily.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset Custom Font Family");
        }
    }



    #endregion



    #region 系统视觉效果



    /// <summary>
    /// 透明/动画效果
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private async void Hyperlink_VisualEffects_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        await Launcher.LaunchUriAsync(new Uri("ms-settings:easeofaccess-visualeffects"));
    }



    #endregion



}
