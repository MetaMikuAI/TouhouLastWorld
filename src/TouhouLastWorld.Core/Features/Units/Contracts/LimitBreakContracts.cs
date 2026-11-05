using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalLimitBreak(int UnitId, int CurrentStep, int NextStep, int RequiredLevel, int GoldCost, int MaterialCount, int ItemId, int DestinyPerItem, int DestinyMaximum);
