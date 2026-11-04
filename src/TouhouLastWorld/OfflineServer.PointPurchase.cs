using System.Text.Json.Nodes;

namespace TouhouLastWorld;

public sealed record LocalPointPurchase(int Level, int PurchaseCount, int Cost, int Base, int PerLevel, int Maximum);

public sealed partial class OfflineServer
{
    private LocalReply BuyPoints(LocalPointPurchase? rule)
    {
        if (rule is null || rule.Cost < 0 || rule.Base < 0 || rule.PerLevel < 0 || rule.Maximum <= 0)
            return new(503, null, "Point purchase configuration unavailable.");
        var points = user["phantasm_point"]!.AsObject();
        var count = points["purchase_count"]!.GetValue<int>();
        var level = user["level"]!.GetValue<int>();
        if (level <= 0 || rule.Level != level || count < 0 || count == int.MaxValue || rule.PurchaseCount != count)
            return new(409, null, "Point purchase state changed.");
        var amount = (long)rule.Base + (long)level * rule.PerLevel;
        var balance = points["points"]!.GetValue<int>();
        if (amount <= 0) return new(503, null, "Invalid point purchase amount.");
        if (balance < 0 || amount > (long)rule.Maximum - balance)
            return new(409, null, "Point capacity exceeded.");
        var updated = user.DeepClone().AsObject();
        var orb = updated["orb"]!.AsObject();
        var free = orb["free"]!.GetValue<long>();
        var purchased = orb["purchased"]!.GetValue<long>();
        if (free < 0 || purchased < 0 || (decimal)free + purchased < rule.Cost)
            return new(409, null, "Insufficient local balance.");
        var freeSpent = Math.Min(free, rule.Cost);
        orb["free"] = free - freeSpent;
        orb["purchased"] = purchased - (rule.Cost - freeSpent);
        updated["phantasm_point"]!["points"] = (int)(balance + amount);
        updated["phantasm_point"]!["purchase_count"] = count + 1;
        Save(updated);
        return new(0, new()
        {
            ["user_update"] = new JsonObject { ["orb"] = orb.DeepClone(), ["phantasm_point"] = updated["phantasm_point"]!.DeepClone() }
        });
    }
}
