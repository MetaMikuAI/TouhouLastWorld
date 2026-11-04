using System.Globalization;
using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalBoxRenovation(int CurrentLevel, int NextLevel, int RequiredLevel, int GoldCost, int Seconds);

public sealed partial class OfflineServer
{
    private LocalReply StartBoxRenovation(string section, LocalBoxRenovation? rule)
    {
        if (user[section] is not JsonObject box)
            return new(503, null, "Local facility unavailable.");
        var level = box["level"]!.GetValue<int>();
        if (rule is null || level <= 0 || level == int.MaxValue || rule.CurrentLevel != level || rule.NextLevel != level + 1
            || rule.RequiredLevel <= 0 || rule.GoldCost < 0 || rule.Seconds < 0)
            return new(503, null, "Next facility level unavailable.");
        if (!string.IsNullOrEmpty(box["renovation_finish_time"]?.GetValue<string>()))
            return new(409, null, "Renovation already in progress.");
        if (user["level"]!.GetValue<int>() < rule.RequiredLevel)
            return new(409, null, "Required account level not reached.");
        var gold = user["gold"]!.GetValue<int>();
        if (gold < rule.GoldCost) return new(409, null, "Insufficient gold.");
        var updated = user.DeepClone().AsObject();
        updated["gold"] = gold - rule.GoldCost;
        updated[section]!["renovation_finish_time"] = DateTimeOffset.UtcNow.AddSeconds(rule.Seconds)
            .ToString("O", CultureInfo.InvariantCulture);
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["gold"] = gold - rule.GoldCost, [section] = user[section]!.DeepClone() }
        });
    }

    private LocalReply CompleteBoxRenovation(string section, LocalArguments? request)
    {
        if (user[section] is not JsonObject box)
            return new(503, null, "Local facility unavailable.");
        var finish = box["renovation_finish_time"]?.GetValue<string>();
        if (string.IsNullOrEmpty(finish)) return new(409, null, "No renovation in progress.");
        if (!DateTimeOffset.TryParse(finish, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var deadline))
            return new(503, null, "Invalid renovation time.");
        if (deadline > DateTimeOffset.UtcNow) return new(409, null, "Renovation is not finished.");
        var level = box["level"]!.GetValue<int>();
        if (level <= 0 || level == int.MaxValue || request?.Value != level || request.EntityId != (long)level + 1)
            return new(503, null, "Next facility level unavailable.");
        var updated = user.DeepClone().AsObject();
        var gold = section == "gold_box";
        var stockKey = gold ? GoldStock : SpiritStock;
        var income = gold ? request.GoldIncome : request.SpiritIncome;
        AccrueBox(updated, section, stockKey, income);
        if (income is not null && updated[stockKey + "_at"]?.GetValue<long>() <= income.At)
            updated[stockKey + "_at"] = income.At;
        updated[section]!["level"] = level + 1;
        updated[section]!["renovation_finish_time"] = "";
        Save(updated);
        return new(0, new() { ["user_update"] = new JsonObject { [section] = user[section]!.DeepClone() } });
    }
}
