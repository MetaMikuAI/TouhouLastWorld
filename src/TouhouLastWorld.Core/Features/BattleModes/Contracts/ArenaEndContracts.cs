using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalArenaScore(int AttackCount, int GrazeCount, int Total, int Attack, int Graze, int Defeated);
public sealed record LocalArenaReward(int Id, int Score, int Value);
public sealed record LocalArenaDailyReward(int Difficulty, LocalInventoryReward[]? Rewards);
public sealed record LocalArenaDaily(int Day, LocalArenaDailyReward[] Stages);
public sealed record LocalArenaEnd(int Result, int BattleNumber, long[] Defeated, bool ExtraTeams, LocalArenaScore? Score, LocalArenaReward[]? Rewards, int Capacity, LocalArenaDaily? Daily = null);
