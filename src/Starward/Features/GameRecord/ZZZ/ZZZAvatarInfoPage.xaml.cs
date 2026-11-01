using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Core.GameRecord;
using Starward.Controls;
using Starward.Core.GameRecord.ZZZ.AvatarInfo;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using Windows.Storage.Streams;
using System.Threading.Tasks;
using Windows.UI;
using Colors = Microsoft.UI.Colors;


namespace Starward.Features.GameRecord.ZZZ;

public sealed partial class ZZZAvatarInfoPage : PageBase
{

    private readonly ILogger<ZZZAvatarInfoPage> _logger = AppConfig.GetLogger<ZZZAvatarInfoPage>();


    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();



    public ZZZAvatarInfoPage()
    {
        this.InitializeComponent();
        MarqueeText1.SizeChanged += (_, _) =>
        {
            UpdateMarqueeLayout();
            StartMarqueeAnimation();
        };
    }



    private GameRecordRole gameRole;



    partial void OnCurrentAvatarChanged(ZZZAvatarDetail value)
    {
        _ = UpdateRankIconsAsync(value?.Rank ?? 0);
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
        await InitializeDataAsync();
        if (AvatarList.Count == 0)
        {
            await RefreshAvatarInfoAsync();
        }
    }



    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        AvatarList = new List<ZZZAvatarInfo>();
        CurrentAvatar = null;
    }




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
    /// 是否正在刷新
    /// </summary>
    [ObservableProperty]
    private bool isRefreshing;



    /// <summary>
    /// 全部代理人信息（未筛选）
    /// </summary>
    private List<ZZZAvatarInfo> _allAvatarInfos = new();


    /// <summary>
    /// 属性筛选（element_type）
    /// </summary>
    private int? _filterElement;


    /// <summary>
    /// 特性筛选（avatar_profession）
    /// </summary>
    private int? _filterProfession;



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



    private void ApplyFilter()
    {
        try
        {
            BuildFilterPanels();
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
            CurrentAvatar = filtered.FirstOrDefault(x => x.Avatar == current)?.Avatar
                            ?? filtered.FirstOrDefault(x => x.Avatar is not null)?.Avatar;
            Image_Emoji.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateFilterButton();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply ZZZ avatar filter ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }



    /// <summary>
    /// 属性图标文件
    /// </summary>
    private static readonly (int Id, string File)[] ElementIcons =
    {
        (200, "attribute-physical-icon.a657c07a.png"),
        (201, "attribute-fire-icon.aeddecee.png"),
        (202, "attribute-ice-icon.5c85742d.png"),
        (203, "attribute-electric-icon.ad4c441f.png"),
        (204, "attribute-wind-icon.c5358f7e.png"),
        (205, "attribute-ether-icon.9a1e42a1.png"),
        (300, "attribute-light-icon.3434f5b5.png"),
    };


    /// <summary>
    /// 特性图标文件
    /// </summary>
    private static readonly (int Id, string File)[] ProfessionIcons =
    {
        (1, "profession-attack-icon.3c2a053f.png"),
        (2, "profession-breakthrough-icon.84a7f20a.png"),
        (3, "profession-anomaly-icon.cd1b1573.png"),
        (4, "profession-support-icon.9cf39df7.png"),
        (5, "profession-defensive-icon.9bd60af4.png"),
        (6, "profession-rupture-icon.4668f112.png"),
        (7, "profession-fengyu-icon.00032c58.png"),
    };



    /// <summary>
    /// 依据代理人数据构建筛选面板：仅显示拥有对应代理人的属性/特性，
    /// 已知项使用固定图标，数据中新增的自动补充；
    /// 按属性筛选时隐藏该属性下没有代理人的特性，按特性筛选时隐藏该特性下没有代理人的属性
    /// </summary>
    private void BuildFilterPanels()
    {
        var avatarData = _allAvatarInfos.Where(x => x.Avatar is not null).ToList();

        // 属性面板依据特性筛选后的数据构建，特性面板依据属性筛选后的数据构建；
        // 若当前筛选在另一维度下已无对应代理人，则自动取消该筛选
        var elementCandidates = avatarData;
        var professionCandidates = avatarData;
        if (_filterElement is int element)
        {
            professionCandidates = avatarData.Where(x => x.Avatar.ElementType == element).ToList();
        }
        if (_filterProfession is int profession)
        {
            elementCandidates = avatarData.Where(x => x.Avatar.Profession == profession).ToList();
        }
        if (_filterElement is int e && !elementCandidates.Any(x => x.Avatar.ElementType == e))
        {
            _filterElement = null;
            elementCandidates = avatarData;
        }
        if (_filterProfession is int p && !professionCandidates.Any(x => x.Avatar.Profession == p))
        {
            _filterProfession = null;
            professionCandidates = avatarData;
        }

        ElementFilterPanel.Items.Clear();
        var knownElements = ElementIcons
            .Where(f => elementCandidates.Any(x => x.Avatar.ElementType == f.Id))
            .Select(f => (Id: f.Id, Content: (object?)FilterImage($"{ImageUrlBase}/{f.File}")));
        var extraElements = elementCandidates
            .GroupBy(x => x.Avatar.ElementType)
            .Where(g => ElementIcons.All(f => f.Id != g.Key))
            .Select(g => (Id: g.Key, Content: (object?)new TextBlock
            {
                Text = g.Key.ToString(),
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.White),
            }))
            .OrderBy(x => x.Id);
        foreach (var (id, content) in knownElements.Concat(extraElements).OrderBy(x => x.Id))
        {
            AddFilterToggle(ElementFilterPanel, id, content, ElementFilter_Click);
        }
        SetFilterChecked(ElementFilterPanel, _filterElement);

        ProfessionFilterPanel.Items.Clear();
        var knownProfessions = ProfessionIcons
            .Where(f => professionCandidates.Any(x => x.Avatar.Profession == f.Id))
            .Select(f => (Id: f.Id, Content: (object?)FilterImage(ProfessionIconUrl(f.Id))))
            .ToList();
        var extraProfessions = professionCandidates
            .GroupBy(x => x.Avatar.Profession)
            .Where(g => ProfessionIcons.All(f => f.Id != g.Key))
            .Select(g => (Id: g.Key, Content: (object?)new TextBlock
            {
                Text = g.Key.ToString(),
                FontSize = 14,
                Foreground = new SolidColorBrush(Colors.White),
            }))
            .OrderBy(x => x.Id);
        foreach (var (id, content) in knownProfessions.Concat(extraProfessions).OrderBy(x => x.Id))
        {
            AddFilterToggle(ProfessionFilterPanel, id, content, ProfessionFilter_Click);
        }
        SetFilterChecked(ProfessionFilterPanel, _filterProfession);
    }



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




    [RelayCommand]
    private async Task RefreshAvatarInfoAsync()
    {
        if (IsRefreshing || gameRole is null)
        {
            return;
        }
        try
        {
            IsRefreshing = true;
            await _gameRecordService.RefreshZZZAvatarInfoAsync(gameRole);
            await InitializeDataAsync();
        }
        catch (miHoYoApiException ex)
        {
            _logger.LogError(ex, "Refresh ZZZ avatar info ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
            GameRecordPage.HandleMiHoYoApiException(ex);
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
            IsRefreshing = false;
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
    /// 静态素材地址
    /// </summary>
    private const string ImageUrlBase = "https://act.mihoyo.com/app/mihoyo-zzz-game-record/images";


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
    public static ImageSource? SkillIconUrl(int skillType)
    {
        string image = skillType switch
        {
            0 => "Icon_Normal.a9f59c45.png",
            1 => "Icon_Special.e7e386a8.png",
            2 => "Icon_Evade.52148d68.png",
            3 => "Icon_JoyStick.49d89a12.png",
            5 => "Icon_CoreSkill.d6f00ff6.png",
            6 => "Icon_Switch.3c5a6456.png",
            _ => "Icon_Normal.a9f59c45.png",
        };
        return new BitmapImage(new Uri($"{ImageUrlBase}/{image}"));
    }


    /// <summary>
    /// 属性图标
    /// </summary>
    public static ImageSource? ElementIconUrl(int elementType, int subElementType)
    {
        // 特殊子属性有独立图标，其余按属性查找
        string? image = (elementType, subElementType) switch
        {
            (202, 1) => "attribute-frost-icon.8de86b8f.png",
            (205, 2) => "attribute-auricink-icon.bb80b050.png",
            (200, 4) => "attribute-honededge-icon.5c0ed0be.png",
            _ => ElementIcons.FirstOrDefault(x => x.Id == elementType).File,
        };
        return image is null ? null : new BitmapImage(new Uri($"{ImageUrlBase}/{image}"));
    }


    /// <summary>
    /// 特性图标
    /// </summary>
    public static ImageSource? ProfessionIconUrl(int profession)
    {
        string? image = ProfessionIcons.FirstOrDefault(x => x.Id == profession).File;
        return image is null ? null : new BitmapImage(new Uri($"{ImageUrlBase}/{image}"));
    }


    /// <summary>
    /// 技能显示顺序 [普攻, 闪避, 支援技, 特殊技, 连携技, 核心被动]
    /// </summary>
    public static List<ZZZSkill> OrderedSkills(ZZZAvatarDetail? avatar)
    {
        if (avatar?.Skills is null)
        {
            return new List<ZZZSkill>();
        }
        List<ZZZSkill> list = new();
        foreach (int type in new[] { 0, 2, 6, 1, 3, 5 })
        {
            var skill = avatar.Skills.FirstOrDefault(x => x.SkillType == type);
            if (skill is not null)
            {
                list.Add(skill);
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
    /// 影画图标
    /// </summary>
    private static readonly string[] RankIconFiles =
    {
        "401332efd0b741a3",
        "3eaedfe01c3dbaca",
        "44d5d7cbc4a42194",
        "c38e275d1752227c",
        "e38ac6870459d7ff",
        "55832216257906d4",
    };

    /// <summary>
    /// GT-UI 图标资源根地址
    /// </summary>
    private const string GtUiIconBase = "https://act.mihoyo.com/act/gt-ui/assets/icons";

    private static readonly HttpClient RankHttpClient = new();

    private static readonly Dictionary<string, ImageSource> RankIconCache = new();

    private Storyboard? _marqueeStoryboard;



    /// <summary>
    /// 立绘背景字滚动动画（从左到右）
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
    /// 更新影画图标
    /// </summary>
    private async Task UpdateRankIconsAsync(int rank)
    {
        var images = new[] { RankIcon1, RankIcon2, RankIcon3, RankIcon4, RankIcon5, RankIcon6 };
        try
        {
            for (int pos = 1; pos <= 6; pos++)
            {
                images[pos - 1].Source = await GetRankIconAsync(pos, rank >= pos);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Load ZZZ rank icons failed.");
        }
    }


    /// <summary>
    /// 影画图标加载（GT-UI 素材；激活为黄色，未激活为半透明白），带缓存
    /// </summary>
    private static async Task<ImageSource?> GetRankIconAsync(int pos, bool unlocked)
    {
        // WinUI SvgImageSource 不支持 8 位带透明度的十六进制颜色，未激活用 fill-opacity 表达半透明
        string color = unlocked ? "#FFF000" : "#FFFFFF";
        string opacity = unlocked ? "1" : "0.6";
        string key = $"{pos}:{color}:{opacity}";
        if (RankIconCache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        string svg = await RankHttpClient.GetStringAsync($"{GtUiIconBase}/{RankIconFiles[pos - 1]}.svg");
        // WinUI SvgImageSource 不支持 <symbol>/<use>，将 symbol 内容展开为根元素直接子节点
        var symbol = Regex.Match(svg, "<symbol[^>]*viewBox=\"([^\"]+)\"[^>]*>(.*)</symbol>", RegexOptions.Singleline);
        if (symbol.Success)
        {
            svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"{symbol.Groups[1].Value}\">{symbol.Groups[2].Value}</svg>";
        }
        svg = svg.Replace("var(--gti-color, #fff)", color);
        if (opacity != "1")
        {
            svg = svg.Replace("<svg ", $"<svg fill-opacity=\"{opacity}\" ");
        }
        var stream = new InMemoryRandomAccessStream();
        using var writer = new DataWriter(stream);
        writer.UnicodeEncoding = UnicodeEncoding.Utf8;
        writer.WriteString(svg);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        stream.Seek(0);
        var svgSource = new SvgImageSource();
        svgSource.RasterizePixelWidth = 96;
        svgSource.RasterizePixelHeight = 96;
        await svgSource.SetSourceAsync(stream);
        RankIconCache[key] = svgSource;
        return svgSource;
    }


    /// <summary>
    /// 影画列表项图标加载
    /// </summary>
    private async void RankListItemIcon_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is ZZZAvatarRank rank)
        {
            try
            {
                var source = await GetRankIconAsync(rank.Pos, rank.IsUnlocked);
                if (ReferenceEquals(image.DataContext, rank))
                {
                    image.Source = source;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Load ZZZ rank list icon failed.");
            }
        }
    }



    /// <summary>
    /// 影画显示文本，0 时隐藏
    /// </summary>
    public static Visibility RankToVisibility(int rank)
    {
        return rank > 0 ? Visibility.Visible : Visibility.Collapsed;
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
    /// 影画序号文本
    /// </summary>
    public static string RankNumText(int pos)
    {
        return string.Format(Lang.ZZZAvatarInfoPage_RankNum, pos);
    }


    /// <summary>
    /// 套装效果显示
    /// </summary>
    public static Visibility SuitEffectsVisibility(ZZZAvatarDetail? avatar)
    {
        return SuitEffects(avatar).Count > 0 ? Visibility.Visible : Visibility.Collapsed;
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
    /// 音擎区域显示（无音擎时隐藏）
    /// </summary>
    public static Visibility WeaponVisibility(ZZZAvatarDetail? avatar)
    {
        return avatar?.Weapon is null ? Visibility.Collapsed : Visibility.Visible;
    }


    /// <summary>
    /// 驱动盘区域显示（无驱动盘时隐藏）
    /// </summary>
    public static Visibility EquipVisibility(ZZZAvatarDetail? avatar)
    {
        return (avatar?.Equip?.Count ?? 0) > 0 ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// 驱动盘等级文本
    /// </summary>
    public static string EquipLevelText(int level)
    {
        return string.Format(Lang.ZZZAvatarInfoPage_EquipLevel, level);
    }


    /// <summary>
    /// 词条颜色（有效词条为橙色）
    /// </summary>
    public static Brush PropertyValidBrush(bool valid)
    {
        return new SolidColorBrush(valid ? Color.FromArgb(0xFF, 0xF1, 0xAD, 0x3D) : Colors.White);
    }


    /// <summary>
    /// 驱动盘副词条颜色（有效词条为橙色）
    /// </summary>
    public static Brush SubPropertyBrush(bool valid)
    {
        return new SolidColorBrush(valid ? Color.FromArgb(0xFF, 0xF1, 0xAD, 0x3D) : Color.FromArgb(0xA6, 0xFF, 0xFF, 0xFF));
    }


    /// <summary>
    /// 有效词条数文本
    /// </summary>
    public static string HitCountText(ZZZEquipPlanInfo? planInfo)
    {
        return string.Format(Lang.ZZZAvatarInfoPage_EquipValidPropertyHits, planInfo?.ValidPropertyCount ?? 0);
    }


    /// <summary>
    /// 驱动盘评分等级文本（ER_S_Plus → S+，ER_SSS_Plus_Crown → SSS+）
    /// </summary>
    public static string RatingLevelText(ZZZEquipPlanInfo? planInfo)
    {
        return (planInfo?.EquipRating ?? "").Replace("ER_", "").Replace("_Plus", "+").Replace("_Crown", "");
    }


    /// <summary>
    /// 驱动盘评分徽章
    /// </summary>
    public static ImageSource? RatingBadgeUrl(ZZZEquipPlanInfo? planInfo)
    {
        if (string.IsNullOrWhiteSpace(planInfo?.EquipRating))
        {
            return null;
        }
        string name = planInfo.EquipRating.Replace("ER_", "").ToLowerInvariant();
        return new BitmapImage(new Uri($"{ImageUrlBase}/{name}.png"));
    }


    /// <summary>
    /// 驱动盘评分区域背景（按评分着色的渐变）
    /// </summary>
    public static Brush RatingBackgroundBrush(ZZZEquipPlanInfo? planInfo)
    {
        Color color = planInfo?.EquipRating switch
        {
            "ER_B" => Color.FromArgb(0xFF, 0x0A, 0x72, 0xFB),
            "ER_A" => Color.FromArgb(0xFF, 0xFB, 0x0A, 0xCB),
            "ER_SSS_Plus_Crown" => Color.FromArgb(0xFF, 0xFF, 0x79, 0x9F),
            null => Colors.Transparent,
            "" => Colors.Transparent,
            _ => Color.FromArgb(0xFF, 0xFB, 0x65, 0x0A),
        };
        color.A = 0x38;
        var brush = new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(0, 0),
            EndPoint = new Windows.Foundation.Point(0.6, 1),
        };
        brush.GradientStops.Add(new GradientStop { Color = color, Offset = 0 });
        color.A = 0x00;
        brush.GradientStops.Add(new GradientStop { Color = color, Offset = 0.3 });
        brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x1B, 0x1B, 0x1B), Offset = 0.3 });
        return brush;
    }


    /// <summary>
    /// 是否显示驱动盘评分区域
    /// </summary>
    public static Visibility RatingVisibility(ZZZEquipPlanInfo? planInfo)
    {
        return planInfo is { Type: > 0 } ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// 背景大字文本（全部转为大写）
    /// </summary>
    public static string MarqueeText(string? name)
    {
        return name?.ToUpperInvariant() ?? string.Empty;
    }


    /// <summary>
    /// 背景大字颜色（背景色略微提亮）
    /// </summary>
    public static Brush MarqueeBrush(ZZZAvatarDetail? avatar)
    {
        Color color = ParseColor(avatar?.VerticalPaintingColor, Colors.White);
        color.R = (byte)Math.Min(255, color.R + 16);
        color.G = (byte)Math.Min(255, color.G + 16);
        color.B = (byte)Math.Min(255, color.B + 16);
        return new SolidColorBrush(color);
    }


    /// <summary>
    /// 立绘区域背景（角色本身对应配色）
    /// </summary>
    public static Brush PaintingBackgroundBrush(ZZZAvatarDetail? avatar)
    {
        Color color = ParseColor(avatar?.VerticalPaintingColor, Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E));
        return new SolidColorBrush(color);
    }


    /// <summary>
    /// 是否有时装（超过 1 套时显示）
    /// </summary>
    public static Visibility MultipleSkinsVisibility(ZZZAvatarDetail? avatar)
    {
        return (avatar?.SkinList?.Count ?? 0) > 1 ? Visibility.Visible : Visibility.Collapsed;
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
    /// 套装效果文本
    /// </summary>
    public static string SuitOwnText(string name, int own)
    {
        return $"{name} [{Math.Min(own, 4)}/4]";
    }


    /// <summary>
    /// 音擎星级填充颜色
    /// </summary>
    public static Brush WeaponStarBrush(ZZZWeapon? weapon, int star)
    {
        bool filled = (weapon?.Star ?? 0) >= star;
        return new SolidColorBrush(filled ? Color.FromArgb(0xFF, 0xFF, 0xDE, 0x00) : Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    }


    /// <summary>
    /// 去除技能/影画描述中的富文本标记
    /// </summary>
    public static string StripRichText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }
        return Regex.Replace(text, "<[^>]+>", "").Replace("\\n", "\n").Trim();
    }


    private static Color ParseColor(string? hex, Color fallback)
    {
        if (!string.IsNullOrWhiteSpace(hex) && hex.StartsWith('#') && hex.Length is 7 or 9)
        {
            try
            {
                byte a = 0xFF;
                if (hex.Length is 9)
                {
                    a = Convert.ToByte(hex.Substring(1, 2), 16);
                }
                byte r = Convert.ToByte(hex.Substring(hex.Length - 6, 2), 16);
                byte g = Convert.ToByte(hex.Substring(hex.Length - 4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(hex.Length - 2, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            catch
            {
                return fallback;
            }
        }
        return fallback;
    }


    /// <summary>
    /// 是否显示潜能激发状态（技能卡片头部）
    /// </summary>
    public static Visibility AwakenVisibility(ZZZAvatarDetail? avatar)
    {
        return avatar?.SkillAwaken is { HasAwakenSystem: true } ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// 潜能激发状态文本
    /// </summary>
    public static string AwakenText(ZZZAvatarDetail? avatar)
    {
        return avatar?.SkillAwaken is { HasAwakenSystem: true, AwakenLevel: > 0 } ? Lang.ZZZAvatarInfoPage_Unlocked : Lang.ZZZAvatarInfoPage_Locked;
    }


    /// <summary>
    /// 时装卡片透明度（未解锁变暗）
    /// </summary>
    public static double SkinOpacity(bool unlocked)
    {
        return unlocked ? 1 : 0.4;
    }


    /// <summary>
    /// 音擎属性文本
    /// </summary>
    public static string WeaponPropertyText(ZZZEquipProperty? property)
    {
        return property is null ? "" : $"{property.PropertyName} +{property.Base}";
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
    /// 代理人属性每行交替背景色
    /// </summary>
    public static Brush AvatarPropertyRowBrush(int index)
    {
        return index % 2 == 0
            ? new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x16, 0x16))
            : new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0x2A, 0x2A));
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
/// 套装效果条目（合并后的套装信息及套装驱动盘图标）
/// </summary>
public class ZZZSuitEffectItem
{

    public ZZZEquipSuit Suit { get; set; }


    public ImageSource? Icon { get; set; }

}
