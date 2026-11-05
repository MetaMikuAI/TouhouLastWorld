using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalMissionRule(int Id, int Type, int EventId, int Required, bool Active, LocalInventoryReward[] Rewards);
public sealed record LocalMissionClaim(int Id, int All, int Type, int EventId, int ChapterId, LocalMissionRule[] Rules);
public sealed record LocalProgressMission(int Id, int EpisodeId, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups, int Type = 1, int BattleId = 0, int UnitProgressType = 0, int Required = 1);
public sealed record LocalDailyQuestMission(int Id, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups, LocalDailyQuestDay Day, int SwitchingHour);
public sealed record LocalDailyLoginMission(int Id, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups, int SwitchingHour, DateTime Now)
{
    public DateTime DayStart => Now.AddHours(-SwitchingHour).Date.AddHours(SwitchingHour);
}
