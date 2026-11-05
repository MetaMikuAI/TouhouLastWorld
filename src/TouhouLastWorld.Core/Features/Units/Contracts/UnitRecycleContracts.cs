using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalUnitRecycleEntry(long Id, int UnitId, int Rare, int LimitBreak, LocalRecycleGain[] Gains);
public sealed record LocalUnitRecycle(LocalUnitRecycleEntry[] Entries, int GoldMax, int PointMax, int ItemMax);
