using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalFreeItem(int Id, LocalInventoryReward Reward);
public sealed record LocalShopRefresh(int[] Costs, int[] ActiveGoods);
public sealed record LocalGoldExchange(int SlotId, int MasterId, int Count, int Cost, bool Active, LocalInventoryReward Reward);
