using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Starward.Controls;
using Starward.Core;
using Starward.Core.GameRecord;
using Starward.Core.GameRecord.ZZZ.AvatarInfo;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;
using Colors = Microsoft.UI.Colors;


namespace Starward.Features.GameRecord.ZZZ;

public sealed partial class ZZZAvatarInfoPage : PageBase
{

    private readonly ILogger<ZZZAvatarInfoPage> _logger = AppConfig.GetLogger<ZZZAvatarInfoPage>();


    private readonly GameRecordZZZService _gameRecordService = AppConfig.GetService<GameRecordZZZService>();


    private GameRecordRole gameRole;


    public ZZZAvatarInfoPage()
    {
        this.InitializeComponent();
        MarqueeText1.SizeChanged += (_, _) =>
        {
            UpdateMarqueeLayout();
            StartMarqueeAnimation();
        };
    }



    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is GameRecordRole role)
        {
            gameRole = role;
        }
    }



    protected override async void OnLoaded()
    {
        await Task.Delay(16);
        BuildFilterPanels();
        await InitializeDataAsync();
    }



    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        _marqueeStoryboard?.Stop();
        AvatarList = new List<ZZZAvatarInfo>();
        CurrentAvatar = null;
    }



    /// <summary>
    /// 刷新进度文本（刷新时显示，格式为 [已完成数/总数]）
    /// </summary>
    [ObservableProperty]
    private string? refreshProgressText;


    /// <summary>
    /// 展示中的代理人列表（已筛选）
    /// </summary>
    [ObservableProperty]
    private List<ZZZAvatarInfo> avatarList;


    /// <summary>
    /// 当前选中的代理人详情
    /// </summary>
    [ObservableProperty]
    private ZZZAvatarDetail currentAvatar;


    /// <summary>
    /// 当前代理人潜能激发是否激活
    /// </summary>
    [ObservableProperty]
    private bool awakenActivated;


    /// <summary>
    /// 当前代理人的套装效果
    /// </summary>
    [ObservableProperty]
    private List<ZZZSuitEffectItem>? activeSuitEffects;



    partial void OnCurrentAvatarChanged(ZZZAvatarDetail value)
    {
        var effects = SuitEffects(value);
        ActiveSuitEffects = effects.Count > 0 ? effects : null;
        AwakenActivated = value?.Skills?.Any(x => x.AwakenState == "AwakenStateActivated") ?? false;
        UpdateRankIcons(value);
    }




    [RelayCommand]
    private async Task RefreshAvatarInfoAsync()
    {
        if (gameRole is null)
        {
            return;
        }
        try
        {
            RefreshProgressText = null;
            var progress = new Progress<(int Finished, int Total)>(p => RefreshProgressText = $"{p.Finished}/{p.Total}");
            var (succeeded, total, fatalException) = await _gameRecordService.RefreshZZZAvatarInfoAsync(gameRole, progress);
            if (fatalException is miHoYoApiException ex)
            {
                _logger.LogWarning(ex, "Refresh ZZZ avatar info aborted ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
                InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
            }
            else if (fatalException is not null)
            {
                _logger.LogWarning(fatalException, "Refresh ZZZ avatar info aborted ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
                InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, fatalException.Message);
            }
            if (succeeded < total)
            {
                InAppToast.MainWindow?.Warning(Lang.Common_Warning, string.Format(Lang.ZZZAvatarInfoPage_RefreshPartialSucceeded, succeeded, total));
            }
            await InitializeDataAsync();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh ZZZ avatar info ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_AccountError, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Refresh ZZZ avatar info ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Warning(Lang.Common_NetworkError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh ZZZ avatar info ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            InAppToast.MainWindow?.Error(ex);
        }
        finally
        {
            RefreshProgressText = null;
            ResetFilter();
        }
    }



    /// <summary>
    /// 重置筛选状态
    /// </summary>
    private void ResetFilter()
    {
        _filterElement = null;
        _filterProfession = null;
        SetFilterChecked(ElementFilterPanel, null);
        SetFilterChecked(ProfessionFilterPanel, null);
        ApplyFilter();
    }



    /// <summary>
    /// 全部代理人信息
    /// </summary>
    private List<ZZZAvatarInfo> _allAvatarInfos = new();


    /// <summary>
    /// 属性筛选
    /// </summary>
    private int? _filterElement;


    /// <summary>
    /// 特性筛选
    /// </summary>
    private int? _filterProfession;


    /// <summary>
    /// 属性图标映射表
    /// </summary>
    private static readonly (int Id, string Uri)[] ElementIcons =
    {
        (200, "ms-appx:///Assets/Image/attribute-physical-icon.a657c07a.png"), // 物理
        (201, "ms-appx:///Assets/Image/attribute-fire-icon.aeddecee.png"),     // 火
        (202, "ms-appx:///Assets/Image/attribute-ice-icon.5c85742d.png"),      // 冰
        (203, "ms-appx:///Assets/Image/attribute-electric-icon.ad4c441f.png"), // 电
        (204, "ms-appx:///Assets/Image/attribute-wind-icon.c5358f7e.png"),     // 风
        (205, "ms-appx:///Assets/Image/attribute-ether-icon.9a1e42a1.png"),    // 以太
        (300, "ms-appx:///Assets/Image/attribute-light-icon.3434f5b5.png"),    // 流明
    };


    /// <summary>
    /// 特性图标映射表
    /// </summary>
    private static readonly (int Id, string Uri)[] ProfessionIcons =
    {
        (1, "ms-appx:///Assets/Image/profession-attack-icon.3c2a053f.png"),       // 强攻
        (2, "ms-appx:///Assets/Image/profession-breakthrough-icon.84a7f20a.png"), // 击破
        (3, "ms-appx:///Assets/Image/profession-anomaly-icon.cd1b1573.png"),      // 异常
        (4, "ms-appx:///Assets/Image/profession-support-icon.9cf39df7.png"),      // 支援
        (5, "ms-appx:///Assets/Image/profession-defensive-icon.9bd60af4.png"),    // 防护
        (6, "ms-appx:///Assets/Image/profession-rupture-icon.4668f112.png"),      // 命破
        (7, "ms-appx:///Assets/Image/profession-fengyu-icon.00032c58.png"),       // 锋御
    };


    /// <summary>
    /// 加载全部代理人信息并应用筛选
    /// </summary>
    private async Task InitializeDataAsync()
    {
        try
        {
            _allAvatarInfos = await _gameRecordService.GetZZZAvatarInfosAsync(gameRole);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load ZZZ avatar info ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }



    /// <summary>
    /// 按属性/特性筛选代理人
    /// </summary>
    private void ApplyFilter()
    {
        try
        {
            IEnumerable<ZZZAvatarInfo> list = _allAvatarInfos;
            if (_filterElement is int element)
            {
                list = list.Where(x => x.Avatar?.ElementType == element);
            }
            if (_filterProfession is int profession)
            {
                list = list.Where(x => x.Avatar?.Profession == profession);
            }
            var filtered = list.ToList();
            var current = CurrentAvatar;
            AvatarList = filtered;
            CurrentAvatar = filtered.FirstOrDefault(x => x.Avatar == current)?.Avatar ?? filtered.FirstOrDefault(x => x.Avatar is not null)?.Avatar;
            GridView_AvatarList.SelectedItem = CurrentAvatar is null ? null : filtered.FirstOrDefault(x => x.Avatar == CurrentAvatar);
            Image_Emoji.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateFilterButton();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply ZZZ avatar filter ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }



    /// <summary>
    /// 构建属性/特性筛选面板
    /// </summary>
    private void BuildFilterPanels()
    {
        ElementFilterPanel.Items.Clear();
        // 属性筛选按钮
        foreach (var (id, uri) in ElementIcons)
        {
            AddFilterToggle(ElementFilterPanel, id, FilterImage(uri), ElementFilter_Click);
        }
        SetFilterChecked(ElementFilterPanel, _filterElement);

        ProfessionFilterPanel.Items.Clear();
        // 特性筛选按钮
        foreach (var (id, _) in ProfessionIcons)
        {
            AddFilterToggle(ProfessionFilterPanel, id, FilterImage(ProfessionIconUrl(id)), ProfessionFilter_Click);
        }
        SetFilterChecked(ProfessionFilterPanel, _filterProfession);
    }



    /// <summary>
    /// 筛选按钮图标
    /// </summary>
    private static CachedImage FilterImage(object? source)
    {
        return new CachedImage { Width = 26, Height = 26, Source = source };
    }



    private void ElementFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: int id, IsChecked: true } toggle)
        {
            _filterElement = id;
            UncheckOtherFilters(ElementFilterPanel, toggle);
        }
        else
        {
            _filterElement = null;
        }
        ApplyFilter();
    }



    private void ProfessionFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { Tag: int id, IsChecked: true } toggle)
        {
            _filterProfession = id;
            UncheckOtherFilters(ProfessionFilterPanel, toggle);
        }
        else
        {
            _filterProfession = null;
        }
        ApplyFilter();
    }



    private void AddFilterToggle(ItemsControl panel, int id, object? content, RoutedEventHandler handler)
    {
        var toggle = new ToggleButton
        {
            Width = 34,
            Height = 34,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(17),
            Tag = id,
        };
        toggle.Content = content;
        toggle.Click += handler;
        panel.Items.Add(toggle);
    }



    private static void SetFilterChecked(ItemsControl panel, int? activeId)
    {
        foreach (var item in panel.Items)
        {
            if (item is ToggleButton { Tag: int id } toggle)
            {
                toggle.IsChecked = id == activeId;
            }
        }
    }



    private static void UncheckOtherFilters(ItemsControl panel, ToggleButton toggle)
    {
        foreach (var item in panel.Items)
        {
            if (item is ToggleButton other && !ReferenceEquals(other, toggle))
            {
                other.IsChecked = false;
            }
        }
    }



    private static Brush ThemeBrush(string key, string fallbackKey)
    {
        var resources = Application.Current.Resources;
        if (resources.TryGetValue(key, out object value) && value is Brush brush)
        {
            return brush;
        }
        if (resources.TryGetValue(fallbackKey, out object fallback) && fallback is Brush fallbackBrush)
        {
            return fallbackBrush;
        }
        return new SolidColorBrush(Colors.Transparent);
    }



    private void UpdateFilterButton()
    {
        // 无代理人数据时隐藏筛选按钮；筛选后无结果时保留筛选按钮
        FilterButton.Visibility = _allAvatarInfos.Any(x => x.Avatar is not null)
            ? Visibility.Visible
            : Visibility.Collapsed;
        bool hasFilter = _filterElement is not null || _filterProfession is not null;
        FilterButton.Background = ThemeBrush(
            hasFilter ? "AccentFillColorDefaultBrush" : "ControlFillColorDefaultBrush",
            hasFilter ? "SystemControlHighlightAccentBrush" : "SystemControlBackgroundAltMediumBrush");
        if (FilterButton.Content is FontIcon icon)
        {
            icon.Foreground = ThemeBrush(
                hasFilter ? "TextOnAccentFillColorPrimaryBrush" : "TextFillColorSecondaryBrush",
                hasFilter ? "SystemControlHighlightAltAltWhiteBrush" : "SystemControlForegroundBaseMediumBrush");
        }
    }



    private void GridView_AvatarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (e.AddedItems.FirstOrDefault() is ZZZAvatarInfo item)
            {
                CurrentAvatar = item.Avatar;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Select ZZZ avatar ({uid}).", gameRole?.Uid);
        }
    }



    /// <summary>
    /// 获取属性图标
    /// </summary>
    public static ImageSource? ElementIconUrl(int elementType, int subElementType)
    {
        // 变种属性有独立图标（烈霜、玄墨、凛刃、燎源），其余按属性查找
        string? uri = (elementType, subElementType) switch
        {
            (202, 1) => "ms-appx:///Assets/Image/attribute-frost-icon.8de86b8f.png",
            (205, 2) => "ms-appx:///Assets/Image/attribute-auricink-icon.bb80b050.png",
            (200, 4) => "ms-appx:///Assets/Image/attribute-honededge-icon.5c0ed0be.png",
            (201, 5) => "ms-appx:///Assets/Image/attribute-flare-icon.f000b2fe.png",
            _ => ElementIcons.FirstOrDefault(x => x.Id == elementType).Uri,
        };
        return uri is null ? null : new BitmapImage(new Uri(uri));
    }


    /// <summary>
    /// 获取特性图标
    /// </summary>
    public static ImageSource? ProfessionIconUrl(int profession)
    {
        string? uri = ProfessionIcons.FirstOrDefault(x => x.Id == profession).Uri;
        return uri is null ? null : new BitmapImage(new Uri(uri));
    }


    /// <summary>
    /// 影画角标
    /// </summary>
    public static Visibility RankToVisibility(int rank)
    {
        return rank > 0 ? Visibility.Visible : Visibility.Collapsed;
    }




    /// <summary>
    /// 立绘背景字滚动动画
    /// </summary>
    private Storyboard? _marqueeStoryboard;



    /// <summary>
    /// 启动立绘背景字滚动动画（从左到右）
    /// </summary>
    private void StartMarqueeAnimation()
    {
        _marqueeStoryboard?.Stop();
        if (CurrentAvatar is null || MarqueeText1.ActualWidth < 1)
        {
            return;
        }
        double width = MarqueeText1.ActualWidth + MarqueeText2.Margin.Left;
        var animation = new DoubleAnimation
        {
            From = -width,
            To = 0,
            Duration = new Duration(TimeSpan.FromSeconds(Math.Max(5, width / 80))),
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(animation, MarqueePanel);
        Storyboard.SetTargetProperty(animation, "(UIElement.RenderTransform).(TranslateTransform.X)");
        _marqueeStoryboard = new Storyboard();
        _marqueeStoryboard.Children.Add(animation);
        _marqueeStoryboard.Begin();
    }



    /// <summary>
    /// 立绘背景字与立绘图片对齐
    /// </summary>
    private void UpdateMarqueeLayout()
    {
        try
        {
            double width = PaintingImage.ActualWidth;
            double height = PaintingImage.ActualHeight;
            if (width < 1 || height < 1 || MarqueeGrid.Parent is not Grid grid)
            {
                return;
            }
            var point = PaintingImage.TransformToVisual(grid).TransformPoint(new Windows.Foundation.Point(0, 0));
            MarqueeGrid.Margin = new Thickness(point.X, 0, 0, 0);
            MarqueeGrid.Width = width;
            MarqueeGrid.Height = height;
            MarqueeGrid.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, width, height) };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update ZZZ marquee layout failed.");
        }
    }



    /// <summary>
    /// 立绘尺寸变化时重新对齐背景字并重启动画
    /// </summary>
    private void PaintingImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateMarqueeLayout();
        StartMarqueeAnimation();
    }



    /// <summary>
    /// 立绘背景字文本
    /// </summary>
    public static string MarqueeText(string? name)
    {
        return name?.ToUpperInvariant() ?? string.Empty;
    }



    /// <summary>
    /// 立绘背景字颜色
    /// </summary>
    public static Brush MarqueeBrush(ZZZAvatarDetail? avatar)
    {
        Color color = HexColorHelper.ParseColor(avatar?.VerticalPaintingColor, Colors.White);
        color.R = (byte)Math.Min(255, color.R + 16);
        color.G = (byte)Math.Min(255, color.G + 16);
        color.B = (byte)Math.Min(255, color.B + 16);
        return new SolidColorBrush(color);
    }



    /// <summary>
    /// 立绘区域背景
    /// </summary>
    public static Brush PaintingBackgroundBrush(ZZZAvatarDetail? avatar)
    {
        Color color = HexColorHelper.ParseColor(avatar?.VerticalPaintingColor, Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E));
        return new SolidColorBrush(color);
    }






    /// <summary>
    /// 影画图标（按影画 1-6 排列，激活后缀 _on，未激活后缀 _off）
    /// </summary>
    private static readonly string[] RankIconUris =
    {
        "ms-appx:///Assets/Image/401332efd0b741a3",
        "ms-appx:///Assets/Image/3eaedfe01c3dbaca",
        "ms-appx:///Assets/Image/44d5d7cbc4a42194",
        "ms-appx:///Assets/Image/c38e275d1752227c",
        "ms-appx:///Assets/Image/e38ac6870459d7ff",
        "ms-appx:///Assets/Image/55832216257906d4",
    };



    /// <summary>
    /// 更新影画图标
    /// </summary>
    private void UpdateRankIcons(ZZZAvatarDetail? avatar)
    {
        var images = new[] { RankIcon1, RankIcon2, RankIcon3, RankIcon4, RankIcon5, RankIcon6 };
        if (avatar is null)
        {
            foreach (var image in images)
            {
                image.Source = null;
            }
            return;
        }
        int rank = avatar.Rank;
        for (int pos = 1; pos <= 6; pos++)
        {
            images[pos - 1].Source = GetRankIcon(pos, rank >= pos);
        }
    }



    /// <summary>
    /// 影画图标（激活为黄色，未激活为半透明白，由 _on/_off 两态 PNG 表达）
    /// </summary>
    private static ImageSource GetRankIcon(int pos, bool unlocked)
    {
        string uri = $"{RankIconUris[pos - 1]}_{(unlocked ? "on" : "off")}.png";
        return new BitmapImage(new Uri(uri));
    }




    /// <summary>
    /// 代理人属性按每行两个分组，用于交替行背景色
    /// </summary>
    public static List<ZZZAvatarPropertyRow> AvatarPropertyRows(List<ZZZAvatarProperty>? properties)
    {
        var rows = new List<ZZZAvatarPropertyRow>();
        if (properties is not null)
        {
            for (int i = 0; i < properties.Count; i += 2)
            {
                rows.Add(new ZZZAvatarPropertyRow
                {
                    Index = rows.Count,
                    Items = properties.Skip(i).Take(2).ToList(),
                });
            }
        }
        return rows;
    }



    /// <summary>
    /// 代理人属性行交替背景色（静态缓存，避免每次绑定都创建画刷）
    /// </summary>
    private static readonly Brush AvatarPropertyRowBrushEven = new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x16, 0x16));


    private static readonly Brush AvatarPropertyRowBrushOdd = new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0x2A, 0x2A));


    /// <summary>
    /// 代理人属性每行交替背景色
    /// </summary>
    public static Brush AvatarPropertyRowBrush(int index)
    {
        return index % 2 == 0 ? AvatarPropertyRowBrushEven : AvatarPropertyRowBrushOdd;
    }



    /// <summary>
    /// 属性面板基础值
    /// </summary>
    public static string PropertyBase(string? @base)
    {
        return string.IsNullOrWhiteSpace(@base) ? "" : @base;
    }



    /// <summary>
    /// 属性面板加成值
    /// </summary>
    public static string PropertyAdd(string? add)
    {
        return string.IsNullOrWhiteSpace(add) ? "" : $"+{add}";
    }




    /// <summary>
    /// 音擎区域显示（无音擎时隐藏）
    /// </summary>
    public static Visibility WeaponVisibility(ZZZAvatarDetail? avatar)
    {
        return avatar?.Weapon is null ? Visibility.Collapsed : Visibility.Visible;
    }



    /// <summary>
    /// 匹配 span 元素（嵌套时由外向内逐层替换）
    /// </summary>
    private static readonly Regex SpanRegex = new(@"<span([^>]*)>(.*?)</span>", RegexOptions.Compiled | RegexOptions.Singleline);


    /// <summary>
    /// 匹配 style 属性中的行内颜色（#RRGGBB、#RRGGBBAA 或 #RGB，alpha 在末尾）
    /// </summary>
    private static readonly Regex SpanColorRegex = new(@"(?<![a-zA-Z-])color\s*:\s*#((?:[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?)|[0-9A-Fa-f]{3})", RegexOptions.Compiled | RegexOptions.IgnoreCase);


    /// <summary>
    /// 规范化富文本描述（span 行内颜色转为 color 标记，无颜色的 span 去除标记以继承前景色，去除残留的 Term 闭合标签）
    /// </summary>
    public static string NormalizeRichText(string? text)
    {
        // 富文本描述可能含技能按键图标标记，确保图标已注册
        ZZZAvatarSkillIconMap.EnsureRegistered();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }
        string previous;
        do
        {
            previous = text;
            text = SpanRegex.Replace(text, static match =>
            {
                string inner = match.Groups[2].Value;
                var color = SpanColorRegex.Match(match.Groups[1].Value);
                return color.Success ? $"<color=#{color.Groups[1].Value}>{inner}</color>" : inner;
            });
        } while (text != previous);
        return text.Replace("</Term>", "").Trim();
    }



    /// <summary>
    /// 音擎进阶等级填充颜色（静态缓存）
    /// </summary>
    private static readonly Brush WeaponStarBrushFilled = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xDE, 0x00));


    private static readonly Brush WeaponStarBrushEmpty = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));


    /// <summary>
    /// 音擎进阶等级填充颜色
    /// </summary>
    public static Brush WeaponStarBrush(ZZZWeapon? weapon, int star)
    {
        bool filled = (weapon?.Star ?? 0) >= star;
        return filled ? WeaponStarBrushFilled : WeaponStarBrushEmpty;
    }



    /// <summary>
    /// 音擎基础属性文本
    /// </summary>
    public static string WeaponMainPropertyText(ZZZWeapon? weapon)
    {
        return WeaponPropertyText(weapon?.MainProperties?.FirstOrDefault());
    }



    /// <summary>
    /// 音擎高级属性文本
    /// </summary>
    public static string WeaponAdvancedPropertyText(ZZZWeapon? weapon)
    {
        return WeaponPropertyText(weapon?.Properties?.FirstOrDefault());
    }



    /// <summary>
    /// 音擎属性文本
    /// </summary>
    public static string WeaponPropertyText(ZZZEquipProperty? property)
    {
        return property is null ? "" : $"{property.PropertyName} +{property.Base}";
    }




    /// <summary>
    /// 技能类型名称
    /// </summary>
    public static string SkillTypeName(int skillType)
    {
        return skillType switch
        {
            0 => Lang.ZZZAvatarInfoPage_SkillType0,
            1 => Lang.ZZZAvatarInfoPage_SkillType1,
            2 => Lang.ZZZAvatarInfoPage_SkillType2,
            3 => Lang.ZZZAvatarInfoPage_SkillType3,
            5 => Lang.ZZZAvatarInfoPage_SkillType5,
            6 => Lang.ZZZAvatarInfoPage_SkillType6,
            _ => $"skill_type_{skillType}",
        };
    }



    /// <summary>
    /// 技能图标
    /// </summary>
    public static ImageSource? SkillIconUrl(int skillType, int profession)
    {
        string uri = (skillType, profession) switch
        {
            (1, 6) => "ms-appx:///Assets/Image/b66e5b835b99c575.png", // 特殊技（命破，独立图标）
            (0, _) => "ms-appx:///Assets/Image/1f66bafcc1f069c2.png", // 普通攻击
            (1, _) => "ms-appx:///Assets/Image/11ee8bd83f94a1eb.png", // 特殊技
            (2, _) => "ms-appx:///Assets/Image/b15382e2428392f2.png", // 闪避
            (3, _) => "ms-appx:///Assets/Image/25a4b80fcfd80526.png", // 连携技
            (5, _) => "ms-appx:///Assets/Image/40791617886f6731.png", // 核心技
            (6, _) => "ms-appx:///Assets/Image/38b9cdcdee285da4.png", // 支援技
            _ => "ms-appx:///Assets/Image/1f66bafcc1f069c2.png",      // 未知类型回退普通攻击
        };
        return new BitmapImage(new Uri(uri));
    }



    /// <summary>
    /// 技能显示顺序 [普通攻击、闪避、支援技、特殊技、连携技、核心技]
    /// </summary>
    public static List<ZZZSkillView> OrderedSkills(ZZZAvatarDetail? avatar)
    {
        if (avatar?.Skills is null)
        {
            return new List<ZZZSkillView>();
        }
        List<ZZZSkillView> list = new();
        foreach (int type in new[] { 0, 2, 6, 1, 3, 5 })
        {
            var skill = avatar.Skills.FirstOrDefault(x => x.SkillType == type);
            if (skill is not null)
            {
                list.Add(new ZZZSkillView { Skill = skill, Profession = avatar.Profession });
            }
        }
        return list;
    }



    /// <summary>
    /// 技能等级文本，不足 10 级前补 0
    /// </summary>
    public static string SkillLevelText(int level)
    {
        return level < 10 ? $"0{level}" : level.ToString();
    }



    /// <summary>
    /// 是否显示潜能激发（不存在潜能激发系统时隐藏）
    /// </summary>
    public static Visibility AwakenVisibility(ZZZAvatarDetail? avatar)
    {
        return avatar?.SkillAwaken is { HasAwakenSystem: true } ? Visibility.Visible : Visibility.Collapsed;
    }



    /// <summary>
    /// 驱动盘区域显示（无驱动盘时隐藏）
    /// </summary>
    public static Visibility EquipVisibility(ZZZAvatarDetail? avatar)
    {
        return (avatar?.Equip?.Count ?? 0) > 0 ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// 是否显示驱动盘评分区域
    /// </summary>
    public static Visibility RatingVisibility(ZZZEquipPlanInfo? planInfo)
    {
        return planInfo is { Type: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }



    /// <summary>
    /// 有效词条数
    /// </summary>
    public static string HitCount(ZZZEquipPlanInfo? planInfo)
    {
        return (planInfo?.ValidPropertyCount ?? 0).ToString();
    }



    /// <summary>
    /// 养成方案词条及命中次数（统计已装备驱动盘副词条中该词条的词条数，含强化次数）
    /// 养成方案来自米游社
    /// </summary>
    public static List<ZZZPlanPropertyHit> PlanPropertyHits(ZZZAvatarDetail? avatar)
    {
        var result = new List<ZZZPlanPropertyHit>();
        if (avatar?.EquipPlanInfo?.PlanEffectivePropertyList is not { Count: > 0 } planList)
        {
            return result;
        }
        var counts = new Dictionary<int, int>();
        foreach (var equip in avatar.Equip ?? Enumerable.Empty<ZZZEquip>())
        {
            foreach (var property in equip?.Properties ?? Enumerable.Empty<ZZZEquipProperty>())
            {
                // 副词条：level 即该词条的总词条数（含初始词条与强化数）
                counts[property.PropertyId] = counts.GetValueOrDefault(property.PropertyId) + property.Level;
            }
        }
        result.AddRange(planList.Select(p => new ZZZPlanPropertyHit
        {
            PropertyId = p.Id,
            FullName = p.FullName,
            HitCount = counts.GetValueOrDefault(p.Id),
        }));
        return result;
    }



    /// <summary>
    /// 驱动盘评分图标
    /// </summary>
    public static ImageSource? RatingBadgeUrl(ZZZEquipPlanInfo? planInfo)
    {
        if (string.IsNullOrWhiteSpace(planInfo?.EquipRating))
        {
            return null;
        }
        string name = planInfo.EquipRating.Replace("ER_", "").ToLowerInvariant();
        if (!RatingBadgeFiles.TryGetValue(name, out string? uri))
        {
            return null;
        }
        return new BitmapImage(new Uri(uri));
    }



    /// <summary>
    /// 驱动盘评分图片
    /// </summary>
    private static readonly Dictionary<string, string> RatingBadgeFiles = new()
    {
        ["b"] = "ms-appx:///Assets/Image/b.6428930f.png",
        ["a"] = "ms-appx:///Assets/Image/a.94d50077.png",
        ["s"] = "ms-appx:///Assets/Image/s.1b99e936.png",
        ["s_plus"] = "ms-appx:///Assets/Image/s_plus.8426d3ac.png",
        ["ss"] = "ms-appx:///Assets/Image/ss.9aefb415.png",
        ["ss_plus"] = "ms-appx:///Assets/Image/ss_plus.6a01e298.png",
        ["sss"] = "ms-appx:///Assets/Image/sss.c792a8a7.png",
        ["sss_plus"] = "ms-appx:///Assets/Image/sss_plus.6a303d10.png",
        ["sss_plus_crown"] = "ms-appx:///Assets/Image/sss_plus_crown.e0a88067.png",
    };


    /// <summary>
    /// 词条颜色（静态缓存：有效词条为橙色；无效词条为半透明白；主词条为白色）
    /// </summary>
    private static readonly Brush PropertyBrushValid = new SolidColorBrush(Color.FromArgb(0xFF, 0xF1, 0xAD, 0x3D));


    private static readonly Brush PropertyBrushInvalidSub = new SolidColorBrush(Color.FromArgb(0xA6, 0xFF, 0xFF, 0xFF));


    private static readonly Brush PropertyBrushMain = new SolidColorBrush(Colors.White);


    /// <summary>
    /// 词条颜色（有效词条为橙色；无效词条为半透明白；主词条为白色）
    /// </summary>
    public static Brush PropertyBrush(bool valid, bool isSub)
    {
        return valid ? PropertyBrushValid : isSub ? PropertyBrushInvalidSub : PropertyBrushMain;
    }



    /// <summary>
    /// 驱动盘副词条强化文本
    /// </summary>
    public static string EquipAddText(int add)
    {
        return add > 0 ? $"+{add}" : "";
    }



    /// <summary>
    /// 驱动盘副词条强化标记显示
    /// </summary>
    public static Visibility EquipAddVisibility(int add)
    {
        return add > 0 ? Visibility.Visible : Visibility.Collapsed;
    }



    /// <summary>
    /// 驱动盘等级文本
    /// </summary>
    public static string EquipLevelText(int level)
    {
        return string.Format(Lang.ZZZAvatarInfoPage_EquipLevel, level);
    }



    /// <summary>
    /// 代理人/驱动盘属性条目图标加载
    /// </summary>
    private void PropertyIconImage_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is not null)
        {
            object context = image.DataContext;
            int? propertyId = null;
            string variant = "";
            if (context is ZZZAvatarProperty property)
            {
                propertyId = property.PropertyId;
            }
            else if (context is ZZZEquipProperty equipProperty)
            {
                // 图标颜色与词条文字一致：有效词条为橙色（_valid），无效词条为半透明白（_invalid）
                propertyId = equipProperty.PropertyId;
                variant = equipProperty.Valid ? "_valid" : "_invalid";
            }
            else if (context is ZZZPlanPropertyHit planHit)
            {
                propertyId = planHit.PropertyId;
            }
            if (propertyId is int id)
            {
                image.Source = GetPropertyIcon(id, variant);
            }
        }
    }



    /// <summary>
    /// 代理人属性图标（按属性 Id 查找）
    /// </summary>
    private static readonly Dictionary<int, string> PropertyIconFiles = new()
    {
        [1] = "ms-appx:///Assets/Image/317ce2a47d66fd3e",   // 生命值
        [2] = "ms-appx:///Assets/Image/4883b409fd524b6d",   // 攻击力
        [3] = "ms-appx:///Assets/Image/f2a930b4deba8528",   // 防御力
        [4] = "ms-appx:///Assets/Image/ae8b5440f710fad8",   // 冲击力
        [5] = "ms-appx:///Assets/Image/09bfc76f660dd0d1",   // 暴击率
        [6] = "ms-appx:///Assets/Image/80d52f918714bed4",   // 暴击伤害
        [7] = "ms-appx:///Assets/Image/2d4c15cee5a66ebe",   // 异常掌控
        [8] = "ms-appx:///Assets/Image/c44eb009da6c398b",   // 异常精通
        [9] = "ms-appx:///Assets/Image/1b15470e75018348",   // 穿透率
        [11] = "ms-appx:///Assets/Image/f26955cea8f29f4f",  // 能量自动回复
        [19] = "ms-appx:///Assets/Image/b765786bd239a9a8",  // 贯穿力
        [20] = "ms-appx:///Assets/Image/2079bc3eeededc26",  // 闪能自动累积
        [232] = "ms-appx:///Assets/Image/b5cae085c3f59de9", // 穿透值
        [315] = "ms-appx:///Assets/Image/82c00663aae7ab6a", // 物理伤害加成
        [316] = "ms-appx:///Assets/Image/0a25f03245faae72", // 火属性伤害加成
        [317] = "ms-appx:///Assets/Image/9b670b3ef874cdf9", // 冰属性伤害加成
        [318] = "ms-appx:///Assets/Image/fe8a6af1c6dcde66", // 电属性伤害加成
        [319] = "ms-appx:///Assets/Image/17b9af04debf66f8", // 以太伤害加成
        [323] = "ms-appx:///Assets/Image/9c599b19c4fa54fe", // 风属性伤害加成
    };


    /// <summary>
    /// 驱动盘词条图标（按旧版属性 Id 查找）
    /// </summary>
    private static readonly Dictionary<int, string> EquipPropertyIconFiles = new()
    {
        [11102] = "ms-appx:///Assets/Image/317ce2a47d66fd3e", // 生命值
        [11103] = "ms-appx:///Assets/Image/317ce2a47d66fd3e", // 生命值百分比
        [12102] = "ms-appx:///Assets/Image/4883b409fd524b6d", // 攻击力
        [12103] = "ms-appx:///Assets/Image/4883b409fd524b6d", // 攻击力百分比
        [13102] = "ms-appx:///Assets/Image/f2a930b4deba8528", // 防御力
        [13103] = "ms-appx:///Assets/Image/f2a930b4deba8528", // 防御力百分比
        [12202] = "ms-appx:///Assets/Image/ae8b5440f710fad8", // 冲击力
        [20103] = "ms-appx:///Assets/Image/09bfc76f660dd0d1", // 暴击率
        [21103] = "ms-appx:///Assets/Image/80d52f918714bed4", // 暴击伤害
        [23203] = "ms-appx:///Assets/Image/b5cae085c3f59de9", // 穿透值
        [30502] = "ms-appx:///Assets/Image/f26955cea8f29f4f", // 能量自动回复
        [31203] = "ms-appx:///Assets/Image/c44eb009da6c398b", // 异常精通
        [31402] = "ms-appx:///Assets/Image/2d4c15cee5a66ebe", // 异常掌控
        [31503] = "ms-appx:///Assets/Image/82c00663aae7ab6a", // 物理伤害加成
        [31603] = "ms-appx:///Assets/Image/0a25f03245faae72", // 火属性伤害加成
        [31703] = "ms-appx:///Assets/Image/9b670b3ef874cdf9", // 冰属性伤害加成
        [31803] = "ms-appx:///Assets/Image/fe8a6af1c6dcde66", // 电属性伤害加成
        [31903] = "ms-appx:///Assets/Image/17b9af04debf66f8", // 以太伤害加成
        [32303] = "ms-appx:///Assets/Image/9c599b19c4fa54fe", // 风属性伤害加成
    };


    /// <summary>
    /// 属性图标（代理人面板与驱动盘词条共用，按属性 Id 查找；默认白色，_valid 橙色，_invalid 半透明白）
    /// </summary>
    private static ImageSource? GetPropertyIcon(int propertyId, string variant = "")
    {
        if (PropertyIconFiles.TryGetValue(propertyId, out string? uri) || EquipPropertyIconFiles.TryGetValue(propertyId, out uri))
        {
            return new BitmapImage(new Uri($"{uri}{variant}.png"));
        }
        return null;
    }


    /// <summary>
    /// 套装效果（相同套装合并，2 件以上生效，附套装驱动盘图标）
    /// </summary>
    public static List<ZZZSuitEffectItem> SuitEffects(ZZZAvatarDetail? avatar)
    {
        return avatar?.Equip?
            .Where(x => x.EquipSuit is not null && x.EquipSuit.Own >= 2)
            .GroupBy(x => x.EquipSuit.SuitId, (k, g) => new ZZZSuitEffectItem
            {
                Suit = g.First().EquipSuit!,
                Icon = string.IsNullOrWhiteSpace(g.First().Icon) ? null : new BitmapImage(new Uri(g.First().Icon)),
            })
            .ToList() ?? new List<ZZZSuitEffectItem>();
    }



    /// <summary>
    /// 套装件数文本（显示为 [n/4]，超过 4 件按 4 计）
    /// </summary>
    public static string SuitOwnText(string name, int own)
    {
        return $"{name} [{Math.Min(own, 4)}/4]";
    }



    /// <summary>
    /// 套装效果标题（{名称}-{件数}件套效果）
    /// </summary>
    public static string SuitEffectTitle(string name, int count)
    {
        return string.Format(Lang.ZZZAvatarInfoPage_SuitEffectCount, name, count);
    }



    /// <summary>
    /// 套装效果条目显示（达到激活件数且描述非空时显示）
    /// </summary>
    public static Visibility SuitEffectVisibility(int own, int count, string desc)
    {
        return own >= count && !string.IsNullOrWhiteSpace(desc) ? Visibility.Visible : Visibility.Collapsed;
    }




    /// <summary>
    /// 影画列表项图标加载
    /// </summary>
    private void RankListItemIcon_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is ZZZAvatarRank rank)
        {
            image.Source = GetRankIcon(rank.Pos, rank.IsUnlocked);
        }
    }



    /// <summary>
    /// 影画序号文本
    /// </summary>
    public static string RankNumText(int pos)
    {
        return string.Format(Lang.ZZZAvatarInfoPage_RankNum, pos);
    }




    /// <summary>
    /// 是否有时装（超过 1 套时显示）
    /// </summary>
    public static Visibility MultipleSkinsVisibility(ZZZAvatarDetail? avatar)
    {
        return (avatar?.SkinList?.Count ?? 0) > 1 ? Visibility.Visible : Visibility.Collapsed;
    }



    /// <summary>
    /// 时装卡片透明度（未解锁变暗）
    /// </summary>
    public static double SkinOpacity(bool unlocked)
    {
        return unlocked ? 1 : 0.4;
    }

}




/// <summary>
/// 代理人属性行（每行两个属性，附行号用于交替行背景色）
/// </summary>
public class ZZZAvatarPropertyRow
{

    public int Index { get; set; }


    public List<ZZZAvatarProperty> Items { get; set; } = new();


    public ZZZAvatarProperty? Item1 => Items.Count > 0 ? Items[0] : null;


    public ZZZAvatarProperty? Item2 => Items.Count > 1 ? Items[1] : null;

}




/// <summary>
/// 技能条目（附代理人特性，用于命破代理人的特殊技图标）
/// </summary>
public class ZZZSkillView
{

    public ZZZSkill Skill { get; set; }


    public int Profession { get; set; }

}




/// <summary>
/// 养成方案词条 chip（HitCount 为该词条在已装备驱动盘副词条中的命中词条数，含强化次数）
/// </summary>
public class ZZZPlanPropertyHit
{

    /// <summary>
    /// 属性 Id（旧版驱动盘词条体系，用于查找词条图标）
    /// </summary>
    public int PropertyId { get; set; }


    public string FullName { get; set; }


    public int HitCount { get; set; }


    /// <summary>
    /// 命中次数显示（单独渲染，用于橙色高亮）
    /// </summary>
    public string HitCountDisplay => $"×{HitCount}";

}




/// <summary>
/// 套装效果条目（合并后的套装信息及套装驱动盘图标）
/// </summary>
public class ZZZSuitEffectItem
{

    public ZZZEquipSuit Suit { get; set; }


    public ImageSource? Icon { get; set; }

}
