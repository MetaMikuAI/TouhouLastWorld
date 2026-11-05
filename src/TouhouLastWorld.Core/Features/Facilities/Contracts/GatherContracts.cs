using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBoxIncome(int Level, int Interval, int Yield, int Capacity, long At);
