using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalMailRules(int GoldMax, int CrystalMax, int ItemMax, int[]? ItemIds);

public sealed partial class OfflineServer
{
    private LocalReply ReceiveMail(LocalArguments? request)
    {
        var ids = request?.Ids;
        if (ids is null || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            return new(400, null, "Distinct positive mail IDs required.");
        var updated = user.DeepClone().AsObject();
        var mail = updated["_local_mail"] as JsonArray ?? new JsonArray();
        var changed = false;
        foreach (var id in ids)
        {
            var entry = mail.FirstOrDefault(value => value?["id"]?.GetValue<long>() == id);
            if (entry is null) return new(404, null, "Mail not found.");
            if (entry["open_type"]?.GetValue<int>() > 0) continue;
            if (entry["receive_grace"]?.GetValue<int>() is not <= 0)
                return new(503, null, "Timed mail settlement unavailable.");
            if (entry["present_list"] is not JsonArray presents)
                return new(503, null, "Mail reward data unavailable.");
            foreach (var present in presents)
            {
                var type = present?["object_type"]?.GetValue<int>();
                var count = present?["object_value"]?.GetValue<int>();
                if (count is null or <= 0 || type is not (3 or 6 or 12)
                    || !string.IsNullOrEmpty(present?["custom_parameter"]?.GetValue<string>()))
                    return new(503, null, "Mail reward type or parameters unsupported.");
                var rules = request!.MailRules;
                if (rules is null) return new(503, null, "Mail reward limits unavailable.");
                if (type == 12)
                {
                    var itemId = present!["object_id"]?.GetValue<int>();
                    if (itemId is null or <= 0 || rules.ItemIds is null || !rules.ItemIds.Contains(itemId.Value) || rules.ItemMax <= 0)
                        return new(503, null, "Mail item definition unavailable.");
                    var inventory = updated["item"]!["item_list"]!.AsArray();
                    var item = inventory.FirstOrDefault(value => value?["item_id"]?.GetValue<int>() == itemId);
                    var total = (long)(item?["num"]?.GetValue<int>() ?? 0) + count.Value;
                    if (total > rules.ItemMax) return new(409, null, "Mail item exceeds capacity.");
                    if (item is null) inventory.Add(new JsonObject { ["item_id"] = itemId.Value, ["num"] = (int)total });
                    else item["num"] = (int)total;
                }
                else
                {
                    var key = type == 3 ? "gold" : "crystal";
                    var limit = type == 3 ? rules.GoldMax : rules.CrystalMax;
                    if (limit <= 0) return new(503, null, "Mail currency limit unavailable.");
                    var total = (long)updated[key]!.GetValue<int>() + count.Value;
                    if (total > limit) return new(409, null, "Mail currency exceeds capacity.");
                    updated[key] = (int)total;
                }
            }
            entry["open_type"] = 1;
            entry["operated_at"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            changed = true;
        }
        if (changed) Save(updated);
        return new(0, new()
        {
            ["mail"] = mail.DeepClone(), ["unread_mail_num"] = mail.Count(entry => (entry?["open_type"]?.GetValue<int>() ?? 0) <= 0),
            ["not_receive_message"] = "", ["receive_unit_list"] = new JsonArray(),
            ["user_update"] = new JsonObject
            {
                ["gold"] = updated["gold"]!.DeepClone(), ["crystal"] = updated["crystal"]!.DeepClone(),
                ["item"] = updated["item"]!.DeepClone()
            }
        });
    }
}
