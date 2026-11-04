using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalRetryFix(int GachaId, int UnitId, int Capacity, bool Active);

public sealed partial class OfflineServer
{
    private LocalReply FixRetryDraw(LocalArguments? request)
    {
        if (request?.Value is not > 0) return new(400, null, "Positive selection identifier required.");
        var entries = user["retry_gacha_count"]!.AsArray();
        var matches = entries.Where(row => row?["gacha_id"]?.GetValue<int>() == request.Value).ToArray();
        if (matches.Length != 1) return new(404, null, "Pending retry selection not found.");
        var entry = matches[0]!;
        var key = request.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var units = user["unit"]!["unit_list"]!.AsArray();
        if (entry["is_fix"]?.GetValue<int>() == 1)
        {
            var receipt = user["_local_retry_receipts"]?[key]?.GetValue<long>();
            return receipt is > 0 && units.Any(unit => unit!["user_unit_id"]!.GetValue<long>() == receipt)
                ? RetryReceipt(receipt.Value) : new(409, null, "Selection already confirmed without an available receipt.");
        }
        var rule = request.RetryFix;
        if (rule is null || rule.GachaId != request.Value || rule.UnitId != entry["object_id"]?.GetValue<int>()
            || rule.UnitId != unitTemplate["unit_id"]!.GetValue<int>() || rule.Capacity <= 0
            || entry["object_type"]?.GetValue<int>() != 9 || entry["is_fix"]?.GetValue<int>() != 0)
            return new(503, null, "Retry selection definition unavailable.");
        if (!rule.Active) return new(1006, null, "Selection is no longer active.");
        if (units.Count >= rule.Capacity || LastUnitId == long.MaxValue)
            return new(409, null, "Unit capacity exceeded.");
        var updated = user.DeepClone().AsObject();
        var uniqueId = LastUnitId + 1;
        var unit = unitTemplate.DeepClone().AsObject();
        unit["user_unit_id"] = uniqueId;
        unit["is_new"] = 1;
        updated["unit"]!["unit_list"]!.AsArray().Add(unit);
        var album = updated["album"]!["unit_list"]!.AsArray();
        if (album.All(row => row?["unit_id"]?.GetValue<int>() != rule.UnitId))
            album.Add(new JsonObject { ["unit_id"] = rule.UnitId, ["talk_rank"] = 0, ["voice_set_id"] = 0,
                ["character_episode_ids"] = new JsonArray(), ["home_motion_ids"] = new JsonArray() });
        updated["retry_gacha_count"]!.AsArray().Single(row => row!["gacha_id"]!.GetValue<int>() == request.Value)!["is_fix"] = 1;
        var receipts = updated["_local_retry_receipts"] as JsonObject ?? new JsonObject();
        if (receipts.Parent is null) updated["_local_retry_receipts"] = receipts;
        receipts[key] = uniqueId;
        updated["_local_unit_sequence"] = uniqueId;
        Save(updated);
        return RetryReceipt(uniqueId);
    }

    private LocalReply RetryReceipt(long id) => new(0, new()
    {
        ["unique_id"] = id, ["user_update"] = new JsonObject { ["unit"] = user["unit"]!.DeepClone(),
            ["album"] = user["album"]!.DeepClone(), ["retry_gacha_count"] = user["retry_gacha_count"]!.DeepClone() }
    });
}
