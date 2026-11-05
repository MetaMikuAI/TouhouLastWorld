using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalContactDetails(string? Name, string? ZipCode, string? Address, string? Phone);
