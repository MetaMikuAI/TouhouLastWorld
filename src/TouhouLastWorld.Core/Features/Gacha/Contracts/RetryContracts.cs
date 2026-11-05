using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalRetryFix(int GachaId, int UnitId, int Capacity, bool Active);
