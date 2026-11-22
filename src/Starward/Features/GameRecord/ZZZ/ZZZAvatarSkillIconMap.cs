using Starward.Controls;
using System;


namespace Starward.Features.GameRecord.ZZZ;

/// <summary>
/// 绝区零「我的代理人」页面技能按键图标注册（IconMap 标记 → 图片地址，渲染为 ColorfulTextBlock 内联图标）
/// </summary>
internal static class ZZZAvatarSkillIconMap
{



    /// <summary>
    /// 技能按键图标
    /// </summary>
    private static readonly (string Tag, string Uri)[] IconMapIcons =
    [
        ("Icon_Normal", "ms-appx:///Assets/Image/a9f59c456b378432b44fd274bed17433_902086071606102520.png"),           // 普通攻击
        ("Icon_Evade", "ms-appx:///Assets/Image/52148d687b0fafdccbd660b921de8622_9122119582802330661.png"),           // 闪避
        ("Icon_Special", "ms-appx:///Assets/Image/e7e386a844a372d7f6b870356014d7e9_2667105461772073027.png"),         // 特殊技
        ("Icon_SpecialReady", "ms-appx:///Assets/Image/99a1443403a77a1cfc8c9f3e33fd1e40_8952618275647366019.png"),    // 强化特殊技
        ("Icon_SpecialReady_Rp", "ms-appx:///Assets/Image/5f2c0762f48aed11ce422b9b7a231c0d_4933917712664360831.png"), // 强化特殊技（命破，独立图标）
        ("Icon_Switch", "ms-appx:///Assets/Image/3c5a6456d767b48d3cee842339be557c_4733793921082135297.png"),          // 连携技
        ("Icon_UltimateReady", "ms-appx:///Assets/Image/b750178d73f7ae221c3c921ba4673ced_5148848264313088273.png"),   // 终结技
    ];


    static ZZZAvatarSkillIconMap()
    {
        foreach (var (tag, uri) in IconMapIcons)
        {
            ColorfulTextBlock.IconMap[tag] = new Uri(uri);
        }
    }


    /// <summary>
    /// 确保技能按键图标已注册
    /// </summary>
    public static void EnsureRegistered() { }


}
