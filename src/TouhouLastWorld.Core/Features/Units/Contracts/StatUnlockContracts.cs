using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalStatUnlock(int CurrentStep, int NextStep, int Cost, int RequiredValue, bool Enabled);
