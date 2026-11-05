using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBoxRenovation(int CurrentLevel, int NextLevel, int RequiredLevel, int GoldCost, int Seconds);
