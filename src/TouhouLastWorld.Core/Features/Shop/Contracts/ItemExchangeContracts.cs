using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalItemExchange(int Id, int SourceId, int Cost, int TargetId, int Capacity);
