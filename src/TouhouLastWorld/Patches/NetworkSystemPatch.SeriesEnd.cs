namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadSeriesEnd(SeriesBattleEndRequest? request)
    {
        var current = User.This()?.series_battle_status;
        var master = MasterDB.This();
        var episode = current == null ? null : master?.Episode?.Find(current.episode_id);
        var group = episode == null ? null : master?.SeriesBattleStageGroup?.Find(episode.series_battle_stage_group_id);
        if (request?.battle_end == null || current == null || group == null || current.difficulty is < 0 or > 2) return null;
        var stageId = current.difficulty switch { 1 => group.hard_stage_master_id, 2 => group.lunatic_stage_master_id,
            _ => group.normal_stage_master_id };
        var stage = master?.SeriesBattleStage?.Find(stageId);
        if (stage == null || current.progress is < 1 or > 5) return null;
        var battles = new[] { stage.battle1_id, stage.battle2_id, stage.battle3_id, stage.battle4_id, stage.battle5_id };
        var count = battles.TakeWhile(id => id > 0).Count();
        if (current.progress > count || battles.Skip(count).Any(id => id != 0)) return null;
        var battle = master?.SeriesBattleBattle?.Find(battles[current.progress - 1]);
        var setting = master?.Setting;
        if (battle == null || setting == null) return null;
        LocalInventoryReward Reward(int type, int id, int value) => new(type, id, value, type switch
        {
            3 => setting.GOLD_MAX, 6 => setting.CRYSTAL_MAX,
            12 when master?.Item?.Find(id) != null => setting.ITEM_MAX, _ => 0
        });
        LocalInventoryReward[]? rewards = null;
        if (battle.player_exp == 0 && battle.unit_exp == 0 && battle.gold_lower_limit >= 0
            && battle.gold_lower_limit == battle.gold_upper_limit && battle.drop_group1_id == 0
            && battle.drop_group2_id == 0 && battle.drop_group3_id == 0 && battle.drop_group4_id == 0
            && battle.destiny_drop_group_id == 0)
            rewards = battle.gold_lower_limit == 0 ? [] : [Reward(3, 0, battle.gold_lower_limit)];
        var first = new[]
        {
            Reward(stage.first_clear_reward1_object_type, stage.first_clear_reward1_object_id, stage.first_clear_reward1_object_value),
            Reward(stage.first_clear_reward2_object_type, stage.first_clear_reward2_object_id, stage.first_clear_reward2_object_value),
            Reward(stage.first_clear_reward3_object_type, stage.first_clear_reward3_object_id, stage.first_clear_reward3_object_value),
            Reward(stage.first_clear_reward4_object_type, stage.first_clear_reward4_object_id, stage.first_clear_reward4_object_value)
        }.Where(row => row.Type != 0 || row.Count != 0).ToArray();
        return new(SeriesEnd: new(current.episode_id, current.difficulty, current.progress, count,
            request.battle_end.result, rewards, first));
    }
}
