using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBatchQuest(int Id, int UnitId, int Limit, bool Eligible);
public sealed record LocalBatchSearch(LocalBatchQuest[] Quests, LocalInventoryReward[] Rewards);
