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


    /// <summary>
    /// 名称
    /// </summary>
    [JsonPropertyName("name_mi18n")]
    public string Name { get; set; }


    /// <summary>
    /// 全名
    /// </summary>
    [JsonPropertyName("full_name_mi18n")]
    public string FullName { get; set; }


    /// <summary>
    /// 属性
    /// </summary>
    [JsonPropertyName("element_type")]
    public int ElementType { get; set; }


    /// <summary>
    /// 阵营
    /// </summary>
    [JsonPropertyName("camp_name_mi18n")]
    public string CampName { get; set; }


    /// <summary>
    /// 特性
    /// </summary>
    [JsonPropertyName("avatar_profession")]
    public int Profession { get; set; }


    /// <summary>
    /// 稀有度
    /// </summary>
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; }


    /// <summary>
    /// 阵营图标
    /// </summary>
    [JsonPropertyName("group_icon_path")]
    public string GroupIconPath { get; set; }


    /// <summary>
    /// 头像
    /// </summary>
    [JsonPropertyName("hollow_icon_path")]
    public string HollowIconPath { get; set; }


    /// <summary>
    /// 影画
    /// </summary>
    [JsonPropertyName("rank")]
    public int Rank { get; set; }


    /// <summary>
    /// 方头像
    /// </summary>
    [JsonPropertyName("role_square_url")]
    public string RoleSquareUrl { get; set; }


    /// <summary>
    /// 变种属性（烈霜、玄墨、凛刃、燎源）
    /// </summary>
    [JsonPropertyName("sub_element_type")]
    public int SubElementType { get; set; }


    /// <summary>
    /// 激发潜能
    /// </summary>
    [JsonPropertyName("awaken_state")]
    public string AwakenState { get; set; }


    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

}
