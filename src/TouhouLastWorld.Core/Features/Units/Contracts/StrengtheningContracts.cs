using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalStatMaterial(int ItemId, int Count, int Stat, int CostPerPoint, int Current, int Maximum);
