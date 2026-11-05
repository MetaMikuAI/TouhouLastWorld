using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPointExchange(int Id, int PointId, int Cost, int Limit, int ObjectType, int ObjectId, int Capacity, bool Active, LocalIntroPicture? Picture = null);
