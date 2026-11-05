using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalSeriesStart(int EpisodeId, int Difficulty, int GroupId, int StageId, int AreaCount, bool Active, int[]? Replays);
