using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalSeriesEnd(int EpisodeId, int Difficulty, int Progress, int AreaCount, int Result, LocalInventoryReward[]? Rewards, LocalInventoryReward[]? FirstRewards);
