using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPointPurchase(int Level, int PurchaseCount, int Cost, int Base, int PerLevel, int Maximum);
