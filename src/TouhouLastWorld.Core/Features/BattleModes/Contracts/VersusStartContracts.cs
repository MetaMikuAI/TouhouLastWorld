using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalVersusRule(int Id, int AddedLevel);
public sealed record LocalVersusStart(int Id, int BaseLevel, int FinalLevel, int TeamNumber, bool Active, int ReleaseEpisode, bool Helper, LocalVersusRule[] Rules);
