using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalCodeExchange(string? Code, string? UserId, string? DeviceId);

public sealed partial class OfflineServer
{
    private static LocalReply CodeReply(int code) => new(0, new() { ["status"] = code == 0, ["code"] = code });

    private LocalReply ExchangeLocalCode(LocalArguments? request)
    {
        var exchange = request?.CodeExchange;
        if (exchange is null || string.IsNullOrWhiteSpace(exchange.Code) || exchange.Code.Any(char.IsControl)
            || account is null || exchange.UserId != account.UserId || exchange.DeviceId != account.DeviceId)
            return CodeReply(1002);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exchange.Code)));
        var updated = user.DeepClone().AsObject();
        var entry = updated["_local_exchange_codes"]?[hash];
        if (entry is null) return CodeReply(1002);
        if (entry["received"]?.GetValue<bool>() == true) return CodeReply(1001);
        var group = entry["group"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(group) && updated["_local_exchange_codes"]!.AsObject().Any(pair =>
                pair.Value?["group"]?.GetValue<string>() == group && pair.Value?["received"]?.GetValue<bool>() == true))
            return CodeReply(1001);
        if (entry["rewards"] is not JsonArray { Count: > 0 } rewards || request?.MailRules is not { } rules)
            return CodeReply(1000);
        foreach (var reward in rewards)
        {
            var type = reward?["object_type"]?.GetValue<int>() ?? 0;
            var id = reward?["object_id"]?.GetValue<int>() ?? 0;
            var count = reward?["object_value"]?.GetValue<int>() ?? 0;
            var capacity = type switch
            {
                3 => rules.GoldMax, 6 => rules.CrystalMax,
                12 when rules.ItemIds?.Contains(id) == true => rules.ItemMax, _ => 0
            };
            if (ApplyInventoryReward(updated, new(type, id, count, capacity)) is not null) return CodeReply(1000);
        }
        entry["received"] = true;
        Save(updated);
        var reply = CodeReply(0);
        reply.Body!["user_update"] = new JsonObject
        {
            ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
            ["item"] = updated["item"]!.DeepClone()
        };
        return reply;
    }
}
