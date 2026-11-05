using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDestinyMaterial(int ItemId, int Count, int Value);
public sealed record LocalDestinyGrowth(int UnitId, int Current, int Maximum, LocalDestinyMaterial[] Materials);
