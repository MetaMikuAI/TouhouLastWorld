using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTowerEnd(int TowerId, int BattleId, int Floor, int MaxFloor, int Result, string? Units, string? Enemies, long[]? Defeated, int[] EnemyIds, LocalInventoryReward[]? FirstRewards, LocalInventoryReward[]? RepeatRewards);
