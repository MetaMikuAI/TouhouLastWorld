using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalIdleSet(long ObjectId, int ObjectType, int Slot, int Content, int Cancel, int Capacity = 0, long Seconds = 0, string? GrowthField = null, int CurrentValue = 0, bool Eligible = false);
