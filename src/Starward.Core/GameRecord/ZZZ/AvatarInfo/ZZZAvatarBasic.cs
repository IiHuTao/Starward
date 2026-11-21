using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Starward.Core.GameRecord.ZZZ.AvatarInfo;

/// <summary>
/// 我的代理人列表（avatar/basic 接口返回）
/// </summary>
public class ZZZAvatarBasicList
{

    [JsonPropertyName("avatar_list")]
    public List<ZZZAvatarBasic> AvatarList { get; set; }

}



/// <summary>
/// 代理人概况
/// </summary>
public class ZZZAvatarBasic
{

    [JsonPropertyName("id")]
    public int Id { get; set; }


    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonPropertyName("name_mi18n")]
    public string Name { get; set; }


    [JsonPropertyName("full_name_mi18n")]
    public string FullName { get; set; }


    [JsonPropertyName("element_type")]
    public int ElementType { get; set; }


    [JsonPropertyName("camp_name_mi18n")]
    public string CampName { get; set; }


    [JsonPropertyName("avatar_profession")]
    public int Profession { get; set; }


    /// <summary>
    /// 稀有度 S A
    /// </summary>
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; }


    /// <summary>
    /// 阵营图标
    /// </summary>
    [JsonPropertyName("group_icon_path")]
    public string GroupIconPath { get; set; }


    /// <summary>
    /// 属性图标
    /// </summary>
    [JsonPropertyName("hollow_icon_path")]
    public string HollowIconPath { get; set; }


    /// <summary>
    /// 影画
    /// </summary>
    [JsonPropertyName("rank")]
    public int Rank { get; set; }


    /// <summary>
    /// 是否为当前使用的代理人
    /// </summary>
    [JsonPropertyName("is_chosen")]
    public bool IsChosen { get; set; }


    [JsonPropertyName("role_square_url")]
    public string RoleSquareUrl { get; set; }


    [JsonPropertyName("sub_element_type")]
    public int SubElementType { get; set; }


    [JsonPropertyName("awaken_state")]
    public string AwakenState { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
