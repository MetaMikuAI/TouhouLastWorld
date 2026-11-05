using System;

using System.Collections;

using System.Globalization;


using BepInEx.Unity.IL2CPP.Utils.Collections;

using HarmonyLib;

using Il2CppInterop.Runtime;


using Applet;


using static TouhouLastWorld.Patches.HarmonyPrefixFlow;

using NativeRoutine = Il2CppSystem.Collections.IEnumerator;

using PacketCallback = Il2CppSystem.Action<int, string>;

using TextCallback = Il2CppSystem.Action<string>;

namespace TouhouLastWorld.Patches;

public static partial class NetworkRequestReader
{
    private static RequestDraft? ReadBattleStartArguments(RequestPacket request)
    {
        var api = "battle_start";
        RequestDraft? arguments = null;
        if (api == "battle_start") arguments = new(QuestBattleStart: ReadQuestBattleStart(request.TryCast<BattleStartRequest>()));

        return arguments;
    }
    private static RequestDraft? ReadBattleEndArguments(RequestPacket request)
    {
        var api = "battle_end";
        RequestDraft? arguments = null;
        if (api == "battle_end") arguments = new(QuestBattleEnd: ReadQuestBattleEnd(request.TryCast<BattleEndRequest>()));

        return arguments;
    }
    private static RequestDraft? ReadGetClearHistoryArguments(RequestPacket request)
    {
        var api = "get_clear_history";
        RequestDraft? arguments = null;
        if (api == "get_clear_history" && request.TryCast<ClearHistoryRequest>() is { } clearHistory)
            arguments = new(HistoryQuery: new(clearHistory.kind, clearHistory.uniq_id1, clearHistory.uniq_id2, 0, null));

        return arguments;
    }
    private static RequestDraft? ReadGetClearHistoryPlayerinfoArguments(RequestPacket request)
    {
        var api = "get_clear_history_playerinfo";
        RequestDraft? arguments = null;
        if (api == "get_clear_history_playerinfo") arguments = ReadHistoryPlayer(request.TryCast<ClearHistoryPlayerInfoRequest>()?.player_id);

        return arguments;
    }
    private static RequestDraft? ReadFriendGetHistoryArguments(RequestPacket request)
    {
        var api = "friend_get_history";
        RequestDraft? arguments = null;
        if (api == "friend_get_history") arguments = ReadHistoryPlayer(request.TryCast<FriendGetHistoryRequest>()?.player_id);

        return arguments;
    }
    private static RequestDraft? ReadGetSeriesBattleRankingTeaminfoArguments(RequestPacket request)
    {
        var api = "get_series_battle_ranking_teaminfo";
        RequestDraft? arguments = null;
        if (api == "get_series_battle_ranking_teaminfo") arguments = ReadRankingHistory(request.TryCast<SeriesBattleRankingGetTeamInfoRequest>());

        return arguments;
    }
    private static RequestDraft? ReadGetClearHistoryTeaminfoArguments(RequestPacket request)
    {
        var api = "get_clear_history_teaminfo";
        RequestDraft? arguments = null;
        if (api == "get_clear_history_teaminfo")
        {
            var query = request.TryCast<ClearHistoryTeamInfoRequest>();
            var master = MasterDB.This();
            var units = master?.Unit?.ToList()?.ToArray();
            var costumes = master?.Costume?.ToList()?.ToArray();
            var pictures = master?.Picture?.ToList()?.ToArray();
            if (query != null)
                arguments = new RequestDraft(HistoryQuery: new(query.kind, query.uniq_id1, query.uniq_id2, query.type, query.player_id),
                    HistoryDefinitions: units == null || costumes == null || pictures == null ? null
                        : new(units.Select(row => row.id).ToArray(), costumes.ToDictionary(row => row.id, row => row.unit_id),
                            pictures.Select(row => row.id).ToArray()));
        }

        return arguments;
    }
    private static RequestDraft? ReadAutoReplayStartArguments(RequestPacket request)
    {
        var api = "auto_replay_start";
        RequestDraft? arguments = null;
        if (api == "auto_replay_start")
        {
            var change = request.TryCast<AutoReplayStartRequest>();
            if (change != null) arguments = new RequestDraft(Value: change.execute_num,
                ReplayMeasure: new(change.quest_type, change.target_id, change.difficulty));
        }

        return arguments;
    }
    private static RequestDraft? ReadEpisodeEndArguments(RequestPacket request)
    {
        var api = "episode_end";
        RequestDraft? arguments = null;
        if (api == "episode_end")
        {
            var episode = request.TryCast<EpisodeEndRequest>();
            var table = MasterDB.This()?.Episode;
            arguments = episode == null ? null : new RequestDraft(EntityId: episode.episode_id,
                Value: table == null ? null : table.Find(episode.episode_id)?.type ?? 0);
        }

        return arguments;
    }
    private static RequestDraft? ReadGetReplayInfoArguments(RequestPacket request)
    {
        var api = "get_replay_info";
        RequestDraft? arguments = null;
        if (api is "get_replay_info" or "update_replay_info")
        {
            var replay = request.TryCast<BattleReplayInfoRequest>();
            arguments = replay == null ? null : new RequestDraft(Text: request.TryCast<UpdateReplayInfoRequest>()?.replay_info,
                ReplayMeasure: new(replay.quest_type, replay.target_id, replay.difficulty));
        }

        return arguments;
    }
    private static RequestDraft? ReadUpdateReplayInfoArguments(RequestPacket request)
    {
        var api = "update_replay_info";
        RequestDraft? arguments = null;
        if (api is "get_replay_info" or "update_replay_info")
        {
            var replay = request.TryCast<BattleReplayInfoRequest>();
            arguments = replay == null ? null : new RequestDraft(Text: request.TryCast<UpdateReplayInfoRequest>()?.replay_info,
                ReplayMeasure: new(replay.quest_type, replay.target_id, replay.difficulty));
        }

        return arguments;
    }
    private static RequestDraft? ReadGetSeriesBattleReplayInfoArguments(RequestPacket request)
    {
        var api = "get_series_battle_replay_info";
        RequestDraft? arguments = null;
        if (api == "get_series_battle_replay_info")
        {
            var query = request.TryCast<SeriesBattleGetReplayInfoRequest>();
            arguments = query == null ? null : new RequestDraft(EntityId: query.stage_id,
                Ids: query.area_index?.ToArray().Select(area => (long)area).ToArray());
        }

        return arguments;
    }
    private static RequestDraft? ReadUpdateSeriesBattleReplayInfoArguments(RequestPacket request)
    {
        var api = "update_series_battle_replay_info";
        RequestDraft? arguments = null;
        if (api == "update_series_battle_replay_info")
        {
            var update = request.TryCast<SeriesBattleUpdateReplayInfoRequest>();
            arguments = update == null ? null : new RequestDraft(update.stage_id, update.area_index, Text: update.replay_info);
        }

        return arguments;
    }
    private static RequestDraft? ReadGetAutoReplayInfoArguments(RequestPacket request)
    {
        var api = "get_auto_replay_info";
        RequestDraft? arguments = null;
        if (api == "get_auto_replay_info")
        {
            var query = request.TryCast<GetAutoReplayInfoRequest>();
            arguments = query == null ? null : new RequestDraft(ReplayMeasure: new(query.quest_type, query.target_id, query.difficulty));
        }

        return arguments;
    }
    private static RequestDraft? ReadGetMultipleSpiritsWeeklyReplayInfoArguments(RequestPacket request)
    {
        var api = "get_multiple_spirits_weekly_replay_info";
        RequestDraft? arguments = null;
        if (api == "get_multiple_spirits_weekly_replay_info")
            arguments = new RequestDraft(EntityId: request.TryCast<GetVersusReplayInfoRequest>()?.multiple_spirits_weekly_master_id);

        return arguments;
    }
    private static RequestDraft? ReadUpdateMultipleSpiritsWeeklyReplayInfoArguments(RequestPacket request)
    {
        var api = "update_multiple_spirits_weekly_replay_info";
        RequestDraft? arguments = null;
        if (api == "update_multiple_spirits_weekly_replay_info")
        {
            var update = request.TryCast<UpdateVersusReplayInfoRequest>();
            arguments = update == null ? null : new RequestDraft(ReplayContent: new(update.multiple_spirits_weekly_master_id,
                update.level, update.add_rules, update.replay_info));
        }

        return arguments;
    }
    private static RequestDraft? ReadUpdateAutoReplayInfoArguments(RequestPacket request)
    {
        var api = "update_auto_replay_info";
        RequestDraft? arguments = null;
        if (api == "update_auto_replay_info")
        {
            var update = request.TryCast<UpdateAutoReplayInfoRequest>();
            arguments = update == null ? null : new RequestDraft(ReplayMeasure: new(update.quest_type, update.target_id,
                update.difficulty, update.win_rate, update.action_num));
        }

        return arguments;
    }
}
