using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalDispatchMember(long Id, int UnitId, int Level, bool Allowed);
public sealed record LocalDispatchStart(int DispatchId, int Seconds, int Cost, int Capacity, int RequiredCount, int RequiredLevel, int QualifiedCount, LocalDispatchMember[] Members);
