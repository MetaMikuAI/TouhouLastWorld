using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalVersusReward(int Level, LocalInventoryReward? FirstReward, bool HasDrops);
public sealed record LocalVersusEnd(int Id, int BaseLevel, int FinalLevel, int Result, int[] Rules, long[] Defeated, bool Ranking, LocalVersusReward[]? Rewards);
