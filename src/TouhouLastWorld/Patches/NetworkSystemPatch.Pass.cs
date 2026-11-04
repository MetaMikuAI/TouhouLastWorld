namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadPassRewards(CollectRewardRequest? request)
    {
        if (request == null) return null;
        var arguments = new LocalArguments(request.level, request.is_all, Text: request.reward_type);
        var master = MasterDB.This();
        var setting = master?.Setting;
        var rows = master?.PassCard?.ToList()?.ToArray();
        if (setting == null || rows == null || rows.Any(row => row.id != row.level)) return arguments;
        LocalInventoryReward ReadReward(PassCardFree reward) => new(reward.type, reward.id, reward.value, reward.type switch
        {
            3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
            12 when master?.Item?.Find(reward.id) != null => setting.ITEM_MAX, _ => 0
        });
        return arguments with { PassRewards = rows.Select(row => new LocalPassReward(row.level,
            row.GetFreeParam()?.free_data?.ToArray().Select(ReadReward).ToArray() ?? Array.Empty<LocalInventoryReward>(),
            row.GetVipParam()?.pay_data?.ToArray().Select(ReadReward).ToArray() ?? Array.Empty<LocalInventoryReward>())).ToArray() };
    }
}
