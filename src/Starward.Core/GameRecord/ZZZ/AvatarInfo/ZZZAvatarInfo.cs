using System.Text.Json.Serialization;

namespace Starward.Core.GameRecord.ZZZ.AvatarInfo;

/// <summary>
/// 我的代理人
/// </summary>
public class ZZZAvatarInfo
{

    public long Uid { get; set; }


    public int Id { get; set; }


    public string Name { get; set; }


    /// <summary>
    /// 代理人详情
    /// </summary>
    public string Value { get; set; }


    [JsonIgnore]
    public ZZZAvatarDetail? Avatar { get; set; }

}
