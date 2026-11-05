using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalInventoryReward(int Type, int Id, int Count, int Capacity, int CostumeId = 0);
public sealed record LocalBookReward(int TaskId, int BookId, LocalInventoryReward Reward);
