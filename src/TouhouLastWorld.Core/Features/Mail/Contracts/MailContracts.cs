using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalMailRules(int GoldMax, int CrystalMax, int ItemMax, int[]? ItemIds);
