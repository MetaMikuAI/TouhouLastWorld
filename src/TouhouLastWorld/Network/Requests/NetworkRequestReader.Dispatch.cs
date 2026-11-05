using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadDispatchSettlement(DispatchEndRequest? request)
    {
        if (request == null) return null;
        var arguments = new RequestDraft(request.user_dispatch_id);
        var master = MasterDB.This();
        var row = master?.Dispatch?.Find(Plugin.Server.ActiveDispatchDefinitionId(request.user_dispatch_id));
        var rewards = master?.DispatchReward?.ToList()?.ToArray();
        var memos = master?.DispatchMemo?.ToList()?.ToArray();
        var setting = master?.Setting;
        if (row == null || rewards == null || memos == null || setting == null) return arguments;
        var groups = new[] { row.reward_group1_id, row.reward_group2_id, row.reward_group3_id, row.reward_group4_id, row.reward_group5_id };
        return arguments with
        {
            DispatchSettlement = new(row.id, memos.Where(memo => memo.dispatch_id == row.id).Select(memo => memo.id).ToArray(),
            rewards.Where(reward => groups.Contains(reward.dispatch_reward_group_id)).Select(reward =>
                new LocalInventoryReward(reward.object_type, reward.object_id, 0, reward.object_type switch
                {
                    3 => setting.GOLD_MAX,
                    6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(reward.object_id) != null => setting.ITEM_MAX,
                    _ => 0
                })).ToArray())
        };
    }

    private static RequestDraft? ReadDispatchStart(DispatchStartRequest? request)
    {
        if (request == null) return null;
        var ids = request.user_unit_id_list?.ToArray();
        var arguments = new RequestDraft(request.user_dispatch_id, Ids: ids);
        var master = MasterDB.This();
        var user = User.This();
        var row = master?.Dispatch?.Find(Plugin.Server.DispatchDefinitionId(request.user_dispatch_id));
        var capacity = user?.dispatch == null ? null : master?.DispatchDeparture?.Find(user.dispatch.departure_rank);
        if (row == null || capacity == null || ids == null || user?.unit == null) return arguments;
        var roles = row.need_role_combination_id == 0 ? null : master?.RoleCombination?.Find(row.need_role_combination_id);
        if (row.need_role_combination_id != 0 && roles == null) return arguments;
        var members = new List<LocalDispatchMember>();
        foreach (var id in ids)
        {
            var owned = user.unit.Find(id);
            var definition = owned == null ? null : master?.Unit?.Find(owned.unit_id);
            if (owned == null || definition == null) return arguments;
            var allowed = roles == null || (definition.role switch
            {
                1 => roles.role1,
                2 => roles.role2,
                3 => roles.role3,
                4 => roles.role4,
                5 => roles.role5,
                6 => roles.role6,
                7 => roles.role7,
                8 => roles.role8,
                _ => 0
            }) > 0;
            members.Add(new(id, owned.unit_id, owned.level_add_reincarnation_stage, allowed));
        }
        return arguments with
        {
            DispatchStart = new(row.id, row.need_time, row.consume_phantasm_point,
            capacity.departure_max, row.need_unit_count, row.need_unit_level, row.need_unit_level_count, members.ToArray())
        };
    }
}
