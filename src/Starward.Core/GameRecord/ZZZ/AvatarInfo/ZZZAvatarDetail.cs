using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Starward.Core.GameRecord.ZZZ.AvatarInfo;

/// <summary>
/// 代理人详情（avatar/info 接口返回）
/// </summary>
public class ZZZAvatarInfoList
{

    [JsonPropertyName("avatar_list")]
    public List<ZZZAvatarDetail> AvatarList { get; set; }

}



public class ZZZAvatarDetail : ZZZAvatarBasic
{

    /// <summary>
    /// 驱动盘
    /// </summary>
    [JsonPropertyName("equip")]
    public List<ZZZEquip> Equip { get; set; }


    /// <summary>
    /// 音擎
    /// </summary>
    [JsonPropertyName("weapon")]
    public ZZZWeapon Weapon { get; set; }


    /// <summary>
    /// 属性
    /// </summary>
    [JsonPropertyName("properties")]
    public List<ZZZAvatarProperty> Properties { get; set; }


    /// <summary>
    /// 技能
    /// </summary>
    [JsonPropertyName("skills")]
    public List<ZZZSkill> Skills { get; set; }


    /// <summary>
    /// 影画
    /// </summary>
    [JsonPropertyName("ranks")]
    public List<ZZZAvatarRank> Ranks { get; set; }


    /// <summary>
    /// 立绘
    /// </summary>
    [JsonPropertyName("role_vertical_painting_url")]
    public string RoleVerticalPaintingUrl { get; set; }


    [JsonPropertyName("us_full_name")]
    public string UsFullName { get; set; }


    /// <summary>
    /// 立绘背景色
    /// </summary>
    [JsonPropertyName("vertical_painting_color")]
    public string VerticalPaintingColor { get; set; }


    [JsonPropertyName("skin_list")]
    public List<ZZZSkin> SkinList { get; set; }


    [JsonPropertyName("skill_awaken")]
    public ZZZSkillAwaken SkillAwaken { get; set; }


    /// <summary>
    /// 养成方案信息（评分）
    /// </summary>
    [JsonPropertyName("equip_plan_info")]
    public ZZZEquipPlanInfo EquipPlanInfo { get; set; }

}



/// <summary>
/// 代理人属性
/// </summary>
public class ZZZAvatarProperty
{

    [JsonPropertyName("property_name")]
    public string PropertyName { get; set; }


    [JsonPropertyName("property_id")]
    public int PropertyId { get; set; }


    [JsonPropertyName("base")]
    public string Base { get; set; }


    [JsonPropertyName("add")]
    public string Add { get; set; }


    [JsonPropertyName("final")]
    public string Final { get; set; }

}



/// <summary>
/// 驱动盘属性
/// </summary>
public class ZZZEquipProperty
{

    [JsonPropertyName("property_name")]
    public string PropertyName { get; set; }


    [JsonPropertyName("property_id")]
    public int PropertyId { get; set; }


    [JsonPropertyName("base")]
    public string Base { get; set; }


    [JsonPropertyName("level")]
    public int Level { get; set; }


    /// <summary>
    /// 是否为有效词条
    /// </summary>
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }


    [JsonPropertyName("system_id")]
    public int SystemId { get; set; }


    [JsonPropertyName("add")]
    public int Add { get; set; }

}



/// <summary>
/// 驱动盘
/// </summary>
public class ZZZEquip
{

    [JsonPropertyName("id")]
    public int Id { get; set; }


    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; }


    [JsonPropertyName("icon")]
    public string Icon { get; set; }


    /// <summary>
    /// 稀有度 S A
    /// </summary>
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; }


    [JsonPropertyName("properties")]
    public List<ZZZEquipProperty> Properties { get; set; }


    [JsonPropertyName("main_properties")]
    public List<ZZZEquipProperty> MainProperties { get; set; }


    [JsonPropertyName("equip_suit")]
    public ZZZEquipSuit EquipSuit { get; set; }


    [JsonPropertyName("equipment_type")]
    public int EquipmentType { get; set; }


    [JsonPropertyName("invalid_property_cnt")]
    public int InvalidPropertyCount { get; set; }


    [JsonPropertyName("all_hit")]
    public bool AllHit { get; set; }

}



/// <summary>
/// 驱动盘套装
/// </summary>
public class ZZZEquipSuit
{

    [JsonPropertyName("suit_id")]
    public int SuitId { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; }


    /// <summary>
    /// 该套装已装备件数
    /// </summary>
    [JsonPropertyName("own")]
    public int Own { get; set; }


    [JsonPropertyName("desc1")]
    public string Desc1 { get; set; }


    [JsonPropertyName("desc2")]
    public string Desc2 { get; set; }

}



/// <summary>
/// 音擎
/// </summary>
public class ZZZWeapon
{

    [JsonPropertyName("id")]
    public int Id { get; set; }


    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; }


    /// <summary>
    /// 影画层数
    /// </summary>
    [JsonPropertyName("star")]
    public int Star { get; set; }


    [JsonPropertyName("icon")]
    public string Icon { get; set; }


    /// <summary>
    /// 稀有度 S A
    /// </summary>
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; }


    [JsonPropertyName("properties")]
    public List<ZZZEquipProperty> Properties { get; set; }


    [JsonPropertyName("main_properties")]
    public List<ZZZEquipProperty> MainProperties { get; set; }


    [JsonPropertyName("talent_title")]
    public string TalentTitle { get; set; }


    [JsonPropertyName("talent_content")]
    public string TalentContent { get; set; }


    [JsonPropertyName("profession")]
    public int Profession { get; set; }

}



/// <summary>
/// 技能
/// </summary>
public class ZZZSkill
{

    [JsonPropertyName("level")]
    public int Level { get; set; }


    [JsonPropertyName("skill_type")]
    public int SkillType { get; set; }


    [JsonPropertyName("items")]
    public List<ZZZSkillItem> Items { get; set; }


    [JsonPropertyName("awaken_state")]
    public string AwakenState { get; set; }

}



/// <summary>
/// 技能明细
/// </summary>
public class ZZZSkillItem
{

    [JsonPropertyName("title")]
    public string Title { get; set; }


    [JsonPropertyName("text")]
    public string Text { get; set; }


    [JsonPropertyName("awaken")]
    public bool Awaken { get; set; }

}



/// <summary>
/// 影画
/// </summary>
public class ZZZAvatarRank
{

    [JsonPropertyName("id")]
    public int Id { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; }


    [JsonPropertyName("desc")]
    public string Desc { get; set; }


    [JsonPropertyName("pos")]
    public int Pos { get; set; }


    [JsonPropertyName("is_unlocked")]
    public bool IsUnlocked { get; set; }

}



/// <summary>
/// 代理人时装
/// </summary>
public class ZZZSkin
{

    [JsonPropertyName("skin_id")]
    public int SkinId { get; set; }


    [JsonPropertyName("skin_name")]
    public string SkinName { get; set; }


    [JsonPropertyName("skin_vertical_painting_url")]
    public string SkinVerticalPaintingUrl { get; set; }


    [JsonPropertyName("skin_square_url")]
    public string SkinSquareUrl { get; set; }


    [JsonPropertyName("skin_hollow_icon_path")]
    public string SkinHollowIconPath { get; set; }


    [JsonPropertyName("skin_vertical_painting_color")]
    public string SkinVerticalPaintingColor { get; set; }


    [JsonPropertyName("unlocked")]
    public bool Unlocked { get; set; }


    /// <summary>
    /// 稀有度 S A
    /// </summary>
    [JsonPropertyName("rarity")]
    public string Rarity { get; set; }


    [JsonPropertyName("is_original")]
    public bool IsOriginal { get; set; }

}



/// <summary>
/// 技能觉醒信息
/// </summary>
public class ZZZSkillAwaken
{

    [JsonPropertyName("has_awaken_system")]
    public bool HasAwakenSystem { get; set; }


    [JsonPropertyName("awaken_level")]
    public int AwakenLevel { get; set; }


    [JsonPropertyName("awaken_max_level")]
    public int AwakenMaxLevel { get; set; }


    [JsonPropertyName("skill_awaken_items")]
    public List<ZZZSkillItem> SkillAwakenItems { get; set; }

}



/// <summary>
/// 养成方案信息（含驱动盘评分）
/// </summary>
public class ZZZEquipPlanInfo
{

    [JsonPropertyName("type")]
    public int Type { get; set; }


    [JsonPropertyName("game_default")]
    public ZZZPlanPropertyGroup GameDefault { get; set; }


    [JsonPropertyName("cultivate_info")]
    public ZZZCultivateInfo CultivateInfo { get; set; }


    [JsonPropertyName("custom_info")]
    public ZZZPlanPropertyGroup CustomInfo { get; set; }


    [JsonPropertyName("valid_property_cnt")]
    public int ValidPropertyCount { get; set; }


    [JsonPropertyName("plan_only_special_property")]
    public bool PlanOnlySpecialProperty { get; set; }


    /// <summary>
    /// 评分等级，如 ER_S_Plus
    /// </summary>
    [JsonPropertyName("equip_rating")]
    public string EquipRating { get; set; }


    [JsonPropertyName("plan_effective_property_list")]
    public List<ZZZPlanProperty> PlanEffectivePropertyList { get; set; }


    [JsonPropertyName("equip_rating_score")]
    public double EquipRatingScore { get; set; }

}



/// <summary>
/// 养成方案属性组
/// </summary>
public class ZZZPlanPropertyGroup
{

    [JsonPropertyName("property_list")]
    public List<ZZZPlanProperty> PropertyList { get; set; }

}



/// <summary>
/// 养成方案属性
/// </summary>
public class ZZZPlanProperty
{

    [JsonPropertyName("id")]
    public int Id { get; set; }


    [JsonPropertyName("name")]
    public string Name { get; set; }


    [JsonPropertyName("full_name")]
    public string FullName { get; set; }


    [JsonPropertyName("system_id")]
    public int SystemId { get; set; }


    [JsonPropertyName("is_select")]
    public bool IsSelect { get; set; }

}



/// <summary>
/// 养成方案元数据
/// </summary>
public class ZZZCultivateInfo
{

    [JsonPropertyName("name")]
    public string Name { get; set; }


    [JsonPropertyName("plan_id")]
    public string PlanId { get; set; }


    [JsonPropertyName("is_delete")]
    public bool IsDelete { get; set; }


    [JsonPropertyName("old_plan")]
    public bool OldPlan { get; set; }

}
