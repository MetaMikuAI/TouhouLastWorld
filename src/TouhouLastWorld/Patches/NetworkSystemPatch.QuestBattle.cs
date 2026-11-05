namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalQuestBattleStart? ReadQuestBattleStart(BattleStartRequest? request)
    {
        if (request is null || request.difficulty is < 0 or > 2
            || request.character_quest_id != 0 || request.relic_quest_id != 0
            || request.helper_info_dict != null || FormationArgs.episodeID != request.episode_id
            || FormationArgs.dailyQuestStageID != request.daily_quest_stage_id
            || FormationArgs.activeTeamNumber != request.team_number || FormationArgs.difficulty != request.difficulty)
            return null;

        var formation = FormationBehaviour.This();
        var team = formation?.m_teamData;
        var player = User.This();
        var master = MasterDB.This();
        if (team == null || player?.unit == null || player.playdata == null || master?.Battle == null) return null;
        var slots = Enumerable.Range(0, team.Length).Select(team.GetDisplayUnit)
            .Where(slot => slot?.Unit?.UserUnitId > 0).ToArray();
        var participants = slots.Select(slot => slot.Unit.UserUnitId).Distinct().ToArray();
        // 剧情角色临时加入客户端角色表，固定槽位只参与战斗。
        var owned = slots.Where(slot => !slot.IsFixedMember).Select(slot => slot.Unit.UserUnitId).Distinct().ToArray();
        if (request.daily_quest_stage_id > 0)
        {
            var stage = master.DailyQuestStage?.Find(request.daily_quest_stage_id);
            var quest = stage == null ? null : master.DailyQuest?.Find(stage.daily_quest_id);
            var time = Applet.GameSystem.This()?.Time;
            if (stage == null || quest == null || time == null || quest.required_unit_id != 0
                || quest.GetResetType() != DailyQuestRecord.ResetType.Daily || master.Event == null
                || master.Battle.Find(stage.battle_id) == null || FormationArgs.battleID != stage.battle_id)
                return null;
            var days = new[] { quest.is_open_week0, quest.is_open_week1, quest.is_open_week2, quest.is_open_week3,
                quest.is_open_week4, quest.is_open_week5, quest.is_open_week6 };
            var weekday = time.GetGameDayOfWeek();
            var open = quest.IsBeginHeld() && weekday is >= 0 and <= 6 && days[weekday] > 0;
            var attemptLimit = quest.count + master.Event.GetDailyQuestAddCount(quest);
            return new(0, stage.battle_id, request.difficulty, request.team_number, open, formation!.TotalCost,
                participants, owned, new(quest.id, stage.id, stage.open_level, attemptLimit));
        }

        var episode = master.Episode?.Find(request.episode_id);
        if (episode == null || episode.type != 0 || master.Chapter?.Find(episode.chapter_id)?.type != 1
            || master.Battle.Find(EpisodeBattleId(episode, request.difficulty)) is not { } battle)
            return null;
        var progress = player.playdata.GetEpisodeInfo(episode, (PlayData.Difficulty)request.difficulty);
        return new(episode.id, battle.id, request.difficulty, request.team_number,
            episode.IsOpenedCondition() && progress != null && progress.state >= PlayData.State.New,
            formation!.TotalCost, participants, owned);
    }

    private static LocalQuestBattleEnd? ReadQuestBattleEnd(BattleEndRequest? request)
    {
        if (request?.battle_end is not { } result) return null;
        var defeated = result.defeated_unit_id_list?.ToArray().Select(id => id).ToArray() ?? [];
        return new(request.battle_id, result.result, defeated,
            result.result == 1 ? ReadQuestBattleRewards(request.battle_id) : null,
            result.result == 1 ? ReadQuestClearScore(result) : null);
    }

    private static LocalClearScore? ReadQuestClearScore(BattleEnd result)
    {
        if (result.action_to_clear <= 0 || result.damege_to_clear <= 0
            || FormationArgs.replayMode || FormationArgs.isCostCut || FormationArgs.autoRunMode)
            return null;
        var player = User.This();
        var owned = Plugin.Server.PendingQuestBattleOwnedUnits;
        if (player?.unit == null || owned.Length == 0) return null;
        var units = owned.Select(player.unit.Find).ToArray();
        if (units.Any(unit => unit == null)) return null;
        return new(result.action_to_clear, result.damege_to_clear, units.Select(unit => unit.power).ToArray());
    }

    private static LocalBattleRewards? ReadQuestBattleRewards(int battleId)
    {
        var server = Plugin.Server;
        var master = MasterDB.This();
        var player = User.This();
        var difficulty = server.PendingQuestBattleDifficulty;
        var battle = master?.Battle?.Find(battleId);
        if (difficulty is < 0 or > 2 || battle == null
            || master?.Setting == null || master.PlayerExp == null || master.UnitExp == null || player?.playdata == null)
            return null;
        var rate = 1;
        if (server.PendingDailyQuestStage > 0)
        {
            if (master.DailyQuestStage?.Find(server.PendingDailyQuestStage)?.battle_id != battleId) return null;
        }
        else
        {
            var episode = master.Episode?.Find(server.PendingQuestBattleEpisode);
            if (episode == null || battleId != EpisodeBattleId(episode, difficulty)) return null;
            rate = player.playdata.GetEpisodeInfo(episode, (PlayData.Difficulty)difficulty)?.exp_rate ?? 0;
            if (rate <= 0) return null;
        }
        var playerExp = (long)battle.player_exp * rate;
        var unitExp = (long)battle.unit_exp * rate;
        if (playerExp is < 0 or > int.MaxValue || unitExp is < 0 or > int.MaxValue) return null;
        var challenge = battle.challenge_id == 0 ? null : master.BattleChallenge?.Find(battle.challenge_id);
        if (battle.challenge_id != 0 && (challenge == null || challenge.challenge1_condition_type != 2
            || challenge.challenge2_condition_type != 2 || challenge.challenge3_condition_type != 2))
            return null;
        var defeatLimits = challenge == null ? [] : new[]
        {
            challenge.challenge1_value, challenge.challenge2_value, challenge.challenge3_value
        };
        var playerLevels = master.PlayerExp.ToList().ToArray().Select(row => new LocalBattleLevel(row.level, row.exp)).ToArray();
        var unitLevels = master.UnitExp.ToList().ToArray();
        var growth = new List<LocalBattleUnitGrowth>();
        foreach (var id in server.PendingQuestBattleOwnedUnits)
        {
            var unit = player.unit?.Find(id);
            var definition = unit == null ? null : master.Unit?.Find(unit.unit_id);
            if (unit == null || definition == null) return null;
            var limit = unit.level_max;
            var levels = unitLevels.Where(row => row.exp_id == definition.exp_id && row.level <= limit)
                .Select(row => new LocalBattleLevel(row.level, row.exp)).ToArray();
            growth.Add(new(id, levels));
        }
        return new(battle.gold_lower_limit, battle.gold_upper_limit, master.Setting.GOLD_MAX,
            (int)playerExp, (int)unitExp, defeatLimits, playerLevels, growth.ToArray());
    }

    private static LocalDailyQuestDay? ReadDailyQuestDay()
    {
        var quests = MasterDB.This()?.DailyQuest?.ToList()?.ToArray();
        var time = Applet.GameSystem.This()?.Time;
        if (quests == null || time == null) return null;
        var day = time.GetGameDate();
        return new(day.Year * 10000 + day.Month * 100 + day.Day,
            quests.Where(quest => quest.GetResetType() == DailyQuestRecord.ResetType.Daily).Select(quest => quest.id).ToArray());
    }

    private static int EpisodeBattleId(EpisodeRecord episode, int difficulty) => difficulty switch
    {
        0 => episode.normal_battle_id,
        1 => episode.hard_battle_id,
        2 => episode.lunatic_battle_id,
        _ => 0
    };
}
