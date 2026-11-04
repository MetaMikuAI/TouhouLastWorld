namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadTowerEnd(TowerBattleEndRequest? request)
    {
        var master = MasterDB.This();
        var row = master?.Tower?.Find(Plugin.Server.PendingTowerId);
        var allFloors = master?.Tower?.ToList()?.ToArray();
        var setting = master?.Setting;
        var drops = master?.BattleDrop?.ToList()?.ToArray();
        if (request?.battle_end == null || row == null || row.battle_id != request.battle_id
            || allFloors == null || setting == null || drops == null) return null;
        LocalInventoryReward[]? Rewards(int[] groups)
        {
            var result = new System.Collections.Generic.List<LocalInventoryReward>();
            foreach (var group in groups.Where(id => id != 0))
            {
                var entries = drops.Where(entry => entry.battle_drop_group_id == group).ToArray();
                if (entries.Length != 1 || master?.BattleDropGroup?.Find(group) == null) return null;
                var entry = entries[0];
                var capacity = entry.object_type switch { 3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
                    12 when master?.Item?.Find(entry.object_id) != null => setting.ITEM_MAX, _ => 0 };
                result.Add(new(entry.object_type, entry.object_id, entry.object_value, capacity));
            }
            return result.ToArray();
        }
        var enemies = request.battle_end.enemy_battle_info;
        var enemyIds = (enemies ?? "").Split('\n', System.StringSplitOptions.RemoveEmptyEntries)
            .Select(line => int.TryParse(line.Split(',')[0], out var id) ? id : 0)
            .Where(id => id > 0 && master?.Enemy?.Find(id) != null).Distinct().ToArray();
        return new(TowerEnd: new(row.id, row.battle_id, row.floor, allFloors.Max(entry => entry.floor),
            request.battle_end.result, request.battle_end.tower_unit_info, enemies,
            request.battle_end.defeated_unit_id_list?.ToArray().Select(id => id).ToArray() ?? [], enemyIds,
            Rewards([row.drop_group1_id_for_first, row.drop_group2_id_for_first, row.drop_group3_id_for_first, row.drop_group4_id_for_first]),
            Rewards([row.drop_group1_id_for_repetition, row.drop_group2_id_for_repetition, row.drop_group3_id_for_repetition, row.drop_group4_id_for_repetition])));
    }
}
