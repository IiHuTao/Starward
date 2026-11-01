using System.Text.Json.Serialization;

namespace Starward.Core.GameRecord.ZZZ.AvatarInfo;

/// <summary>
/// 数据库中存储的代理人信息（ZZZAvatarInfo 表）
/// </summary>
public class ZZZAvatarInfo
{

    public long Uid { get; set; }


    public int Id { get; set; }


    public string Name { get; set; }


    /// <summary>
    /// 代理人详情 JSON
    /// </summary>
    public string Value { get; set; }


    /// <summary>
    /// 反序列化后的代理人详情（非数据库字段）
    /// </summary>
    [JsonIgnore]
    public ZZZAvatarDetail? Avatar { get; set; }

}
