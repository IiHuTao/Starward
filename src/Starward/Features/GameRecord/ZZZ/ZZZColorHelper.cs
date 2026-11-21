using System;
using Windows.UI;


namespace Starward.Features.GameRecord.ZZZ;

/// <summary>
/// 米哈游接口颜色解析（#RRGGBB 或 #RRGGBBAA，8 位按 RRGGBBAA 序，与 CSS/Unity 富文本一致）
/// </summary>
internal static class ZZZColorHelper
{


    /// <summary>
    /// 解析 #RRGGBB 或 #RRGGBBAA 颜色，无效时返回 false（当前接口数据均为 6 位，8 位为防御性支持）
    /// </summary>
    public static bool TryParseColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#' || hex.Length is not (7 or 9))
        {
            return false;
        }
        try
        {
            // 接口可能返回小写十六进制，Convert.FromHexString 只接受大写
            byte[] value = Convert.FromHexString(hex.AsSpan(1).ToString().ToUpperInvariant());
            if (value.Length is 3)
            {
                color = Color.FromArgb(0xFF, value[0], value[1], value[2]);
                return true;
            }
            if (value.Length is 4)
            {
                color = Color.FromArgb(value[3], value[0], value[1], value[2]);
                return true;
            }
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }



    /// <summary>
    /// 解析 #RRGGBB 或 #RRGGBBAA 颜色，无效时返回回退颜色
    /// </summary>
    public static Color ParseColor(string? hex, Color fallback)
    {
        return TryParseColor(hex, out Color color) ? color : fallback;
    }

}
