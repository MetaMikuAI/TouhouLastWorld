using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalHistoryQuery(int Kind, int FirstId, int SecondId, int Type, string? PlayerId);
public sealed record LocalHistoryDefinitions(int[] Units, Dictionary<int, int> Costumes, int[] Pictures, int[]? Achievements = null, int[]? StageGroups = null);
public sealed record LocalClearScore(int Actions, long Damage, int[] TeamPowers);
