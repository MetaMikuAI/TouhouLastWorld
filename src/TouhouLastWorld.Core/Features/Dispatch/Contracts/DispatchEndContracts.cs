using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDispatchSettlement(int DispatchId, int[] MemoIds, LocalInventoryReward[] Rewards);
