using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalEventDrop(int Id, int Count, bool Target, LocalInventoryReward Reward);
public sealed record LocalEventPool(int Id, int Step, int Cost, int Total, LocalEventDrop[] Drops);
public sealed record LocalEventDraw(int EventId, int GachaId, int Count, bool Active, LocalEventPool[] Pools);
