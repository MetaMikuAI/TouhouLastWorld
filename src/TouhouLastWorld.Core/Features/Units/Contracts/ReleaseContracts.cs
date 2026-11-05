using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalAutomaticRelease(int Id, int UnitId, int Level, int Stage, bool RequiresFullStats, bool FullStats);
