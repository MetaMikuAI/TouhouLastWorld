using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadVersusEnd(VersusBattleEndRequest? request)
    {
        var end = request?.battle_end;
        if (end == null) return null;
        var master = MasterDB.This();
        var setting = master?.Setting;
        var records = master?.MultipleSpiritsWeeklyReward?.ToList()?.ToArray();
        LocalInventoryReward? Reward(MultipleSpiritsWeeklyRewardRecord row)
        {
            var type = row.first_clear_reward_object_type;
            var id = row.first_clear_reward_object_id;
            var count = row.first_clear_reward_object_value;
            if (type == 0 && id == 0 && count == 0) return null;
            return new(type, id, count, type switch
            {
                3 => setting!.GOLD_MAX,
                6 => setting!.CRYSTAL_MAX,
                12 when master?.Item?.Find(id) != null => setting!.ITEM_MAX,
                _ => 0
            });
        }
        return new(VersusEnd: new(request!.multiple_spirits_weekly_master_id, request.cleared_level,
            request.final_cleared_level, end.result,
            end.cleared_special_rules?.ToArray().Select(id => id).ToArray() ?? [],
            end.defeated_unit_id_list?.ToArray().Select(id => id).ToArray() ?? [], end.reg_ranking != 0,
            setting == null || records == null ? null : records
                .Where(row => row.multiple_spirits_weekly_master_id == request.multiple_spirits_weekly_master_id)
                .Select(row => new LocalVersusReward(row.level, Reward(row), row.drop_group1_id != 0
                    || row.drop_group2_id != 0 || row.drop_group3_id != 0 || row.drop_group4_id != 0
                    || row.destiny_drop_group_id != 0)).ToArray()));
    }
}
