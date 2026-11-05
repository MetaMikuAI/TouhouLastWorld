using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTreasureDraw(int EventId, bool Active, int[] GiftIds);
public sealed record LocalEventRewardGroup(int Id, int Step, int Points, int IconType, LocalInventoryReward[] Rewards);
public sealed record LocalEventRewards(int EventId, LocalEventRewardGroup[] Groups);
