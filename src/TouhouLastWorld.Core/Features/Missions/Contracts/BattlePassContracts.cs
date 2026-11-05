using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBattlePassPeriod(int Id, int InitialLevel);
public sealed record LocalBattlePassTask(int Id, int Required, int Experience, bool Active, LocalInventoryReward[]? Rewards = null);
public sealed record LocalBattlePassDailyTask(LocalBattlePassTask Rule, int CompleteType, string Name, int DisplayOrder, (int Id, int Count)[] RewardGroups);
public sealed record LocalBattlePassDaily(int PassId, LocalDailyQuestDay Day, int SwitchingHour, DateTime Now, LocalBattlePassDailyTask[] Tasks);
public sealed record LocalBattlePassClaim(int Id, int All, int PassId, bool Active, LocalBattlePassTask[] Tasks, Dictionary<int, int> Levels);
public sealed record LocalBattlePassLevelReward(int Level, LocalInventoryReward[]? Normal, LocalInventoryReward[]? Premium, LocalInventoryReward[]? Premium2);
public sealed record LocalBattlePassRewards(int PassId, bool Active, LocalBattlePassLevelReward[] Levels);
