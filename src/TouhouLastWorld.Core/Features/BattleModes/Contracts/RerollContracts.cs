using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalReroll(int Selected, int[] Offered, int Draws, int Rerolls, int[] Definitions);
