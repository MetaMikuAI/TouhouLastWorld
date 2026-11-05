using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft ReadEventRewards(int? eventId)
    {
        var arguments = new RequestDraft(Value: eventId);
        var master = MasterDB.This();
        var setting = master?.Setting;
        var groups = master?.EventTotalPointGroup?.ToList()?.ToArray();
        var rewards = master?.EventTotalPoint?.ToList()?.ToArray();
        if (eventId is null || setting == null || groups == null || rewards == null) return arguments;
        LocalInventoryReward ReadReward(EventTotalPointRecord row) => new(row.object_type, row.object_id, row.object_value,
            row.object_type switch
            {
                3 => setting.GOLD_MAX,
                6 => setting.CRYSTAL_MAX,
                12 when master?.Item?.Find(row.object_id) != null => setting.ITEM_MAX,
                _ => 0
            });
        return arguments with
        {
            EventRewards = new(eventId.Value, groups.Where(row => row.event_id == eventId)
            .Select(row => new LocalEventRewardGroup(row.id, row.step_number, row.total_point, row.icon_type,
                rewards.Where(reward => reward.total_point_group_id == row.id).Select(ReadReward).ToArray())).ToArray())
        };
    }
}
