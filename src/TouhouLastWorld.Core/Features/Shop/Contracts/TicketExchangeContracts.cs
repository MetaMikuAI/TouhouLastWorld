using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalTicketExchange(int ExchangeId, int TicketId, int ItemId, int Cost, int Count, int ObjectId, int Capacity, bool Active, int Parameter1 = 0, int Parameter2 = 0, int Characteristic = 0, int ObjectType = 10, int CostumeId = 0);
