using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalRankMaterial(int UnitId, int Rank, int ObjectType, int ObjectId, int Count);
public sealed record LocalRankPromotion(int UnitId, int Rank, int NextRank, int RequiredLevel, int GoldCost, LocalRankMaterial[] Materials);
