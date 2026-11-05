using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalRecycleGain(int Type, int Id, int Value);
public sealed record LocalPictureRecycleEntry(long Id, int PictureId, int Strength, int LimitBreak, LocalRecycleGain[] Gains);
public sealed record LocalPictureRecycle(LocalPictureRecycleEntry[] Entries, int GoldMax, int PointMax, int ItemMax);
