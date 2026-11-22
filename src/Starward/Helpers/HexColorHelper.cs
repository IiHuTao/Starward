using System;
using Windows.UI;


namespace Starward.Helpers;

/// <summary>
/// 米哈游接口颜色解析（#RRGGBB / #RRGGBBAA / #RGB）
/// </summary>
internal static class HexColorHelper
{


    /// <summary>
    /// 解析 #RRGGBB、#RRGGBBAA 或 #RGB（简写每位展开为两位）颜色，无效时返回 false
    /// </summary>
    public static bool TryParseColor(string? hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex) || hex[0] != '#' || hex.Length is not (4 or 7 or 9))
        {
            return false;
        }
        try
        {
            // #RGB 简写，每位数字展开为两位
            if (hex.Length == 4)
            {
                hex = string.Concat("#", hex[1], hex[1], hex[2], hex[2], hex[3], hex[3]);
            }
            byte[] value = Convert.FromHexString(hex.AsSpan(1));
            if (value.Length == 4)
            {
                color = Color.FromArgb(value[3], value[0], value[1], value[2]);
            }
            else
            {
                color = Color.FromArgb(0xFF, value[0], value[1], value[2]);
            }
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }


    /// <summary>
    /// 解析 #RRGGBB、#RRGGBBAA 或 #RGB 颜色，无效时返回回退颜色
    /// </summary>
    public static Color ParseColor(string? hex, Color fallback)
    {
        return TryParseColor(hex, out Color color) ? color : fallback;
    }

}
