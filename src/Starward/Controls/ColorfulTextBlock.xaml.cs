using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.UI;


namespace Starward.Controls;

/// <summary>
/// 通用富文本描述控件（解析 color 换行斜体标记，IconMap 标记渲染为内联图标，无效或未闭合标记按普通文本降级）
/// </summary>
[INotifyPropertyChanged]
public sealed partial class ColorfulTextBlock : UserControl
{


    public ColorfulTextBlock()
    {
        this.InitializeComponent();
    }


    /// <summary>
    /// IconMap 图标注册表（标记名 → 图片地址），文本中的 &lt;IconMap:标记名&gt; 将渲染为内联图标，由具体游戏注册，未注册时降级为普通文本
    /// </summary>
    public static Dictionary<string, Uri> IconMap { get; } = new();



    public TextWrapping TextWrapping
    {
        get { return (TextWrapping)GetValue(TextWrappingProperty); }
        set { SetValue(TextWrappingProperty, value); }
    }

    // Using a DependencyProperty as the backing store for TextWrapping.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty TextWrappingProperty =
        DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(ColorfulTextBlock), new PropertyMetadata(default));



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
                if (desc[i..].StartsWith("<color=#"))
                {
                    int openEnd = desc[i..].IndexOf('>');
                    if (openEnd > 8
                        && BrushFromHex(desc.Slice(i + 7, openEnd - 7)) is Brush brush
                        && TryParseColorRegion(desc, i + openEnd + 1, brush, out var segments, out int regionEnd))
                    {
                        FlushRun(paragraph, desc, lastIndex, i);
                        foreach (var (inlineText, icon, inlineBrush) in segments)
                        {
                            if (icon is not null)
                            {
                                paragraph.Inlines.Add(new InlineUIContainer { Child = icon });
                            }
                            else
                            {
                                paragraph.Inlines.Add(new Run { Text = inlineText, Foreground = inlineBrush });
                            }
                        }
                        i = regionEnd;
                        lastIndex = i;
                        continue;
                    }
                }
                // 图标
                if (desc[i..].StartsWith("<IconMap:") && desc[i..].IndexOf('>') is int close && close > 9)
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
                    // 空内容（<i></i>）时仅消费标记，不添加空 Run
                    if (italicEnd > 3)
                    {
                        paragraph.Inlines.Add(new Run
                        {
                            Text = desc.Slice(i + 3, italicEnd - 3).ToString(),
                            FontStyle = Windows.UI.Text.FontStyle.Italic,
                        });
                    }
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
    /// 从 color 开标签之后解析到匹配的 &lt;/color&gt;，支持嵌套标记（内层文字使用内层颜色，无效的内层颜色沿用外层）与任意层级的图标标记，未找到配对的 &lt;/color&gt; 时返回 false
    /// </summary>
    private static bool TryParseColorRegion(ReadOnlySpan<char> desc, int start, Brush brush, out List<(string? Text, Image? Icon, Brush Brush)> segments, out int regionEnd)
    {
        segments = new List<(string?, Image?, Brush)>();
        regionEnd = -1;
        // 各层颜色画刷（栈底为最外层），文字段记录当前层的画刷
        var brushes = new List<Brush> { brush };
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
                            segments.Add((desc[runStart..j].ToString(), null, brushes[^1]));
                        }
                        regionEnd = j + 8;
                        return true;
                    }
                    if (j > runStart)
                    {
                        segments.Add((desc[runStart..j].ToString(), null, brushes[^1]));
                    }
                    // 仅当该层有有效颜色时出栈，无效嵌套层的文字沿用外层画刷
                    if (brushes.Count > 1)
                    {
                        brushes.RemoveAt(brushes.Count - 1);
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
                        segments.Add((desc[runStart..j].ToString(), null, brushes[^1]));
                    }
                    // 嵌套颜色有效时入栈供内层文字使用；无效时仅计入层级（保证 </color> 配对），沿用外层画刷
                    if (openEnd > 8 && BrushFromHex(desc.Slice(j + 7, openEnd - 7)) is Brush innerBrush)
                    {
                        brushes.Add(innerBrush);
                    }
                    j += openEnd > 0 ? openEnd + 1 : 1;
                    runStart = j;
                    continue;
                }
                if (desc[j..].StartsWith("<IconMap:") && desc[j..].IndexOf('>') is int close && close > 9
                    && IconMap.TryGetValue(desc.Slice(j + 9, close - 9).ToString(), out var source))
                {
                    if (j > runStart)
                    {
                        segments.Add((desc[runStart..j].ToString(), null, brushes[^1]));
                    }
                    segments.Add((null, new Image
                    {
                        Width = 14,
                        Height = 14,
                        Margin = new Thickness(2, 0, 2, -4),
                        Source = new BitmapImage(source),
                    }, brushes[^1]));
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
    /// 十六进制颜色（#RRGGBB / #RRGGBBAA）转画刷，无效时返回 null
    /// </summary>
    private static Brush? BrushFromHex(ReadOnlySpan<char> hex)
    {
        if (HexColorHelper.TryParseColor(hex.ToString(), out Color color))
        {
            return new SolidColorBrush(color);
        }
        return null;
    }


}
