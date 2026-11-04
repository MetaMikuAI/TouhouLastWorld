using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBoxIncome(int Level, int Interval, int Yield, int Capacity, long At);

public sealed partial class OfflineServer
{
    private const string SpiritStock = "_local_gathering_phantasm_point";
    private const string GoldStock = "_local_gathering_gold";

    private void RefreshFacilityStock(LocalArguments? request)
    {
        if (request?.GoldIncome is null && request?.SpiritIncome is null) return;
        var updated = user.DeepClone().AsObject();
        var goldChanged = AccrueBox(updated, "gold_box", GoldStock, request.GoldIncome);
        var spiritChanged = AccrueBox(updated, "phantasm_box", SpiritStock, request.SpiritIncome);
        if (goldChanged || spiritChanged) Save(updated);
    }

    private static bool AccrueBox(JsonObject updated, string section, string stockKey, LocalBoxIncome? rule)
    {
        if (rule is not { Level: > 0, Interval: > 0, Yield: > 0, Capacity: > 0, At: > 0 }
            || updated[section]?["level"]?.GetValue<int>() != rule.Level)
            return false;
        var timestampKey = stockKey + "_at";
        var timestamp = updated[timestampKey]?.GetValue<long>();
        var last = timestamp ?? rule.At;
        var stock = updated[stockKey]?.GetValue<int>() ?? 0;
        if (last < 0 || last > rule.At || stock < 0) return false;
        var intervals = (rule.At - last) / rule.Interval;
        var remaining = Math.Max(0L, (long)rule.Capacity - stock);
        var units = Math.Min(remaining, Math.Min(intervals, remaining) * rule.Yield);
        var refreshed = stock + (int)units;
        // 库存满时停止累计，收取后从当前时间继续。
        var next = refreshed >= rule.Capacity ? rule.At : last + intervals * rule.Interval;
        if (timestamp == next && refreshed == stock) return false;
        updated[timestampKey] = next;
        updated[stockKey] = refreshed;
        return true;
    }

    private LocalReply GatherPoints(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value != user["level"]?.GetValue<int>())
            return new(503, null, "Current level capacity unavailable.");
        if (user["phantasm_point"] is not JsonObject points)
            return new(503, null, "Local point balance unavailable.");
        var updated = user.DeepClone().AsObject();
        var accrued = AccrueBox(updated, "phantasm_box", SpiritStock, request.SpiritIncome);
        var stock = updated[SpiritStock]?.GetValue<int>() ?? 0;
        var balance = points["points"]!.GetValue<int>();
        if (stock < 0 || balance < 0) return new(503, null, "Invalid local balance.");
        var amount = (int)Math.Min(stock, Math.Max(0, request.EntityId.Value - balance));
        if (amount != 0)
        {
            updated["phantasm_point"]!["points"] = balance + amount;
            updated[SpiritStock] = stock - amount;
        }
        if (accrued || amount != 0) Save(updated);
        return new(0, new()
        {
            ["gathering_phantasm_point"] = stock - amount,
            ["user_update"] = new JsonObject { ["phantasm_point"] = user["phantasm_point"]!.DeepClone() }
        });
    }

    private LocalReply GatherGold(LocalArguments? request)
    {
        if (request?.EntityId is null or <= 0 or > int.MaxValue || request.Value != user["level"]?.GetValue<int>())
            return new(503, null, "Current level capacity unavailable.");
        var updated = user.DeepClone().AsObject();
        var accrued = AccrueBox(updated, "gold_box", GoldStock, request.GoldIncome);
        var stock = updated[GoldStock]?.GetValue<int>() ?? 0;
        var balance = user["gold"]!.GetValue<int>();
        if (stock < 0 || balance < 0) return new(503, null, "Invalid local balance.");
        var amount = (int)Math.Min(stock, Math.Max(0, request.EntityId.Value - balance));
        if (amount != 0)
        {
            updated["gold"] = balance + amount;
            updated[GoldStock] = stock - amount;
        }
        if (accrued || amount != 0) Save(updated);
        return new(0, new()
        {
            ["gathering_gold"] = stock - amount,
            ["user_update"] = new JsonObject { ["gold"] = balance + amount }
        });
    }
}
