using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI;


namespace Starward.Features.GameRecord.ZZZ;

/// <summary>
/// 绝区零富文本描述控件（解析 color 换行斜体标记，IconMap 标记渲染为内联图标）
/// </summary>
[INotifyPropertyChanged]
public sealed partial class ZZZRichTextBlock : UserControl
{


    public ZZZRichTextBlock()
    {
        this.InitializeComponent();
    }


    /// <summary>
    /// 技能按键图标地址
    /// </summary>
    private const string IconMapUrlBase = "https://fastcdn.mihoyo.com/static-resource-v2/2025/12/16";

    /// <summary>
    /// 技能按键图标（IconMap 标记 → 文件名）
    /// </summary>
    private static readonly (string Tag, string File)[] IconMapIcons =
    [
        ("Icon_Normal", "a9f59c456b378432b44fd274bed17433_902086071606102520.png"),
        ("Icon_Evade", "52148d687b0fafdccbd660b921de8622_9122119582802330661.png"),
        ("Icon_Special", "e7e386a844a372d7f6b870356014d7e9_2667105461772073027.png"),
        ("Icon_SpecialReady", "99a1443403a77a1cfc8c9f3e33fd1e40_8952618275647366019.png"),
        ("Icon_SpecialReady_Rp", "5f2c0762f48aed11ce422b9b7a231c0d_4933917712664360831.png"),
        ("Icon_UltimateReady", "b750178d73f7ae221c3c921ba4673ced_5148848264313088273.png"),
        ("Icon_Switch", "3c5a6456d767b48d3cee842339be557c_4733793921082135297.png"),
    ];


    /// <summary>
    /// 注册富文本中使用的技能按键图标（首次使用本控件类型时执行，不依赖调用方先初始化其他类型）
    /// </summary>
    static ZZZRichTextBlock()
    {
        foreach (var (tag, file) in IconMapIcons)
        {
            IconMap[tag] = new Uri($"{IconMapUrlBase}/{file}");
        }
    }


    /// <summary>
    /// IconMap 图标注册表（标记名 → 图片地址），文本中的 &lt;IconMap:标记名&gt; 将渲染为内联图标
    /// </summary>
    public static Dictionary<string, Uri> IconMap { get; } = new();



    public TextWrapping TextWrapping
    {
        get { return (TextWrapping)GetValue(TextWrappingProperty); }
        set { SetValue(TextWrappingProperty, value); }
    }

    // Using a DependencyProperty as the backing store for TextWrapping.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(ZZZRichTextBlock), new PropertyMetadata(default));



    [ObservableProperty]
    public partial string Text { get; set; }
    partial void OnTextChanged(string value)
    {
        try
        {
            var blocks = ThisRichTextBlock.Blocks;
            blocks.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }
            var paragraph = new Paragraph();
            var desc = value.AsSpan();
            int lastIndex = 0;
            int i = 0;
            while (i < desc.Length)
            {
                // 换行
                if (i + 1 < desc.Length && desc[i] == '\\' && desc[i + 1] == 'n')
                {
                    FlushRun(paragraph, desc, lastIndex, i);
                    paragraph.Inlines.Add(new LineBreak());
                    i += 2;
                    lastIndex = i;
                    continue;
                }
                // 颜色
                if (desc[i] == '<' && desc[i..].StartsWith("<color=#"))
                {
                    int openEnd = desc[i..].IndexOf('>');
                    if (openEnd > 8 && TryParseColorRegion(desc, i + openEnd + 1, out var segments, out int regionEnd))
                    {
                        FlushRun(paragraph, desc, lastIndex, i);
                        var brush = BrushFromHex(desc.Slice(i + 7, openEnd - 7));
                        foreach (var (inlineText, icon) in segments)
                        {
                            if (icon is not null)
                            {
                                paragraph.Inlines.Add(new InlineUIContainer { Child = icon });
                            }
                            else
                            {
                                paragraph.Inlines.Add(new Run { Text = inlineText, Foreground = brush });
                            }
                        }
                        i = regionEnd;
                        lastIndex = i;
                        continue;
                    }
                }
                // 图标
                if (desc[i] == '<' && desc[i..].StartsWith("<IconMap:") && desc[i..].IndexOf('>') is int close && close > 9)
                {
                    if (IconMap.TryGetValue(desc.Slice(i + 9, close - 9).ToString(), out var source))
                    {
                        FlushRun(paragraph, desc, lastIndex, i);
                        paragraph.Inlines.Add(new InlineUIContainer
                        {
                            Child = new Image
                            {
                                Width = 14,
                                Height = 14,
                                Margin = new Thickness(2, 0, 2, -4),
                                Source = new BitmapImage(source),
                            },
                        });
                        i += close + 1;
                        lastIndex = i;
                        continue;
                    }
                }
                // 引用
                if (i + 2 < desc.Length && desc[i] == '<' && desc[i + 1] == 'i' && desc[i + 2] == '>' && desc[i..].IndexOf("</i>") is int italicEnd && italicEnd > 2)
                {
                    FlushRun(paragraph, desc, lastIndex, i);
                    paragraph.Inlines.Add(new Run
                    {
                        Text = desc.Slice(i + 3, italicEnd - 3).ToString(),
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                    });
                    i += italicEnd + 4;
                    lastIndex = i;
                    continue;
                }
                i++;
            }
            FlushRun(paragraph, desc, lastIndex, desc.Length);
            blocks.Add(paragraph);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ThisRichTextBlock.Blocks.Clear();
            var fallback = new Paragraph();
            fallback.Inlines.Add(new Run { Text = value });
            ThisRichTextBlock.Blocks.Add(fallback);
        }
    }


    /// <summary>
    /// 追加 [start, end) 区间的普通文本 Run（区间为空时跳过）
    /// </summary>
    private static void FlushRun(Paragraph paragraph, ReadOnlySpan<char> desc, int start, int end)
    {
        if (end > start)
        {
            paragraph.Inlines.Add(new Run { Text = desc[start..end].ToString() });
        }
    }


    /// <summary>
    /// 从 color 开标签之后解析到匹配的 &lt;/color&gt;，支持嵌套标记与内含图标标记，区域为空时返回 false
    /// </summary>
    private static bool TryParseColorRegion(ReadOnlySpan<char> desc, int start, out List<(string? Text, Image? Icon)> segments, out int regionEnd)
    {
        segments = new List<(string?, Image?)>();
        regionEnd = -1;
        int depth = 1;
        int runStart = start;
        int j = start;
        while (j < desc.Length)
        {
            if (desc[j] == '<')
            {
                if (desc[j..].StartsWith("</color>"))
                {
                    depth--;
                    if (depth == 0)
                    {
                        if (j > runStart)
                        {
                            segments.Add((desc[runStart..j].ToString(), null));
                        }
                        regionEnd = j + 8;
                        return true;
                    }
                    if (j > runStart)
                    {
                        segments.Add((desc[runStart..j].ToString(), null));
                    }
                    j += 8;
                    runStart = j;
                    continue;
                }
                if (desc[j..].StartsWith("<color=#"))
                {
                    depth++;
                    int openEnd = desc[j..].IndexOf('>');
                    if (j > runStart)
                    {
                        segments.Add((desc[runStart..j].ToString(), null));
                    }
                    j += openEnd > 0 ? openEnd + 1 : 1;
                    runStart = j;
                    continue;
                }
                if (depth == 1 && desc[j..].StartsWith("<IconMap:") && desc[j..].IndexOf('>') is int close && close > 9
                    && IconMap.TryGetValue(desc.Slice(j + 9, close - 9).ToString(), out var source))
                {
                    if (j > runStart)
                    {
                        segments.Add((desc[runStart..j].ToString(), null));
                    }
                    segments.Add((null, new Image
                    {
                        Width = 14,
                        Height = 14,
                        Margin = new Thickness(2, 0, 2, -4),
                        Source = new BitmapImage(source),
                    }));
                    j += close + 1;
                    runStart = j;
                    continue;
                }
            }
            j++;
        }
        return false;
    }


    /// <summary>
    /// 十六进制颜色（#RRGGBB 或 #RRGGBBAA）转画刷，无效时返回 null
    /// </summary>
    private static Brush? BrushFromHex(ReadOnlySpan<char> hex)
    {
        if (ZZZColorHelper.TryParseColor(hex.ToString(), out Color color))
        {
            return new SolidColorBrush(color);
        }
        return null;
    }


}
