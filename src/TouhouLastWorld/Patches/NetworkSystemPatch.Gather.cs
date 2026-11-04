namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments ReadFacilityIncome(LocalArguments arguments)
    {
        var player = User.This();
        var master = MasterDB.This();
        var gold = master?.GoldBox?.Find(player?.gold_box?.level ?? 0);
        var spirit = master?.PhantasmBox?.Find(player?.phantasm_box?.level ?? 0);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return arguments with
        {
            GoldIncome = gold == null ? null : new(gold.level, gold.gathering_time, gold.gathering_gold, gold.gold_limit_stock, now),
            SpiritIncome = spirit == null ? null : new(spirit.level, spirit.gathering_time, spirit.gathering_phantasm_point,
                spirit.phantasm_point_limit_stock, now)
        };
    }
}
