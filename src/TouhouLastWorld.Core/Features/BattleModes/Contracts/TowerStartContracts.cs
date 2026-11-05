using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTowerStart(int TowerId, int BattleId, int Floor, int TeamNumber, int MaxFloor = int.MaxValue);
