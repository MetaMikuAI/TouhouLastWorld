using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadSeriesStart(SeriesBattleStartRequest? request)
    {
        if (request == null || request.difficulty is < 0 or > 2) return null;
        var master = MasterDB.This();
        var episode = master?.Episode?.Find(request.episode_id);
        var group = episode == null ? null : master?.SeriesBattleStageGroup?.Find(episode.series_battle_stage_group_id);
        var chapter = episode == null ? null : master?.Chapter?.Find(episode.chapter_id);
        var time = Applet.GameSystem.This()?.Time;
        if (episode == null || group == null || chapter == null || time == null) return null;
        if (chapter.event_id > 0 && User.This()?.FindEventInfo(chapter.event_id) == null) return null;
        var stageId = request.difficulty switch
        {
            1 => group.hard_stage_master_id,
            2 => group.lunatic_stage_master_id,
            _ => group.normal_stage_master_id
        };
        var stage = master?.SeriesBattleStage?.Find(stageId);
        if (stage == null) return null;
        var battles = new[] { stage.battle1_id, stage.battle2_id, stage.battle3_id, stage.battle4_id, stage.battle5_id };
        var count = battles.TakeWhile(id => id > 0).Count();
        if (count == 0 || battles.Skip(count).Any(id => id != 0)
            || battles.Take(count).Any(id => master?.SeriesBattleBattle?.Find(id) == null)) return null;
        return new(SeriesStart: new(episode.id, request.difficulty, group.id, stage.id, count,
            episode.IsOpenedCondition(), request.use_replays?.ToArray()));
    }
}
