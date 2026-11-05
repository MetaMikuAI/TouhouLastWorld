using static TouhouLastWorld.Patches.ActivePanels;
using Applet;

namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadEventDraw(EventPointGachaDoAPIRequest? request)
    {
        var master = MasterDB.This();
        var definitions = master?.EventPointGacha?.ToList()?.ToArray();
        var drops = master?.EventPointGachaDrop?.ToList()?.ToArray();
        var setting = master?.Setting;
        var time = GameSystem.This()?.Time;
        if (request == null || definitions == null || drops == null || setting == null || time == null) return null;
        var selected = definitions.FirstOrDefault(row => row.id == request.gacha_id);
        var activity = selected == null ? null : master?.Event?.Find(selected.event_id);
        if (selected == null || activity == null) return null;
        LocalInventoryReward Reward(EventPointGachaDropRecord row) => new(row.object_type, row.object_id, row.object_value,
            row.object_type switch
            {
                3 => setting.GOLD_MAX,
                6 => setting.CRYSTAL_MAX,
                12 when master?.Item?.Find(row.object_id) != null => setting.ITEM_MAX,
                _ => 0
            });
        return new(EventDraw: new(selected.event_id, request.gacha_id, request.count,
            time.IsActiveDate(activity.start_at, activity.end_at), definitions.Where(row => row.event_id == selected.event_id)
                .Select(row => new LocalEventPool(row.id, row.step_number, row.consume_point, row.total_drop_value,
                    drops.Where(drop => drop.drop_group == row.target_reward_drop_group || drop.drop_group == row.drop_group)
                        .Select(drop => new LocalEventDrop(drop.id, drop.drop_value, drop.drop_group == row.target_reward_drop_group, Reward(drop))).ToArray())).ToArray()));
    }
}
