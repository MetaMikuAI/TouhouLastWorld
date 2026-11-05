using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalQuestBattleStart(int EpisodeId, int BattleId, int Difficulty, int TeamNumber, bool Open, int Cost, long[] Participants, long[] OwnedParticipants, LocalDailyQuestEntry? Daily = null);
public sealed record LocalBattleLevel(int Level, int Exp);
public sealed record LocalBattleUnitGrowth(long UnitId, LocalBattleLevel[] Levels);
public sealed record LocalBattleRewards(int GoldMin, int GoldMax, int GoldCapacity, int PlayerExp, int UnitExp, int[] DefeatLimits, LocalBattleLevel[] PlayerLevels, LocalBattleUnitGrowth[] Units);
public sealed record LocalQuestBattleEnd(int BattleId, int Result, long[] Defeated, LocalBattleRewards? Rewards, LocalClearScore? ClearScore = null);
