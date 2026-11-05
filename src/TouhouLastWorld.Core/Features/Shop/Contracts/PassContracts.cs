using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPassTask(int Id, int Experience, int[] Levels, int[] VipLevels);
public sealed record LocalPassReward(int Level, LocalInventoryReward[] Free, LocalInventoryReward[] Vip);
public sealed record LocalPassGoods(int Id, int Cost, int Limit, LocalInventoryReward Reward);
