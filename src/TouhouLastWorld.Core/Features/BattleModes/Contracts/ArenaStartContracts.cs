using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalArenaStart(int Category, int BattleId, int EnemyLevel, int Difficulty, int BattleNumber, int[] Teams);
