using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalCodeExchange(string? Code, string? UserId, string? DeviceId);
