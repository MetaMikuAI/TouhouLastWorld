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
    private static RequestDraft? ReadGetQuestClearedTeamInfoArguments(RequestPacket request)
    {
        var api = "get_quest_cleared_team_info";
        RequestDraft? arguments = null;
        if (api == "get_quest_cleared_team_info")
        {
            var query = request.TryCast<GetQuestClearedTeamInfoRequest>();
            arguments = query == null ? null : new RequestDraft(ReplayMeasure: new(query.quest_type, query.target_id, query.difficulty));
        }

        return arguments;
    }
    private static RequestDraft? ReadSeriesBattleTeamUpdateArguments(RequestPacket request)
    {
        var api = "series_battle_team_update";
        RequestDraft? arguments = null;
        if (api == "series_battle_team_update")
            arguments = new RequestDraft(SeriesTeams: request.TryCast<SeriesBattleTeamUpdateRequest>()?.team_update_list?.ToArray()
                .Select(team => new LocalSeriesTeam(team.team_no, team.user_unit_id_list?.ToArray())).ToArray());

        return arguments;
    }
    private static RequestDraft? ReadRogueliteTopArguments(RequestPacket request)
    {
        var api = "roguelite_top";
        RequestDraft? arguments = null;
        if (api == "roguelite_top")
            arguments = new RequestDraft(Value: MasterDB.This()?.Setting?.MAX_DECK_NO);

        return arguments;
    }
    private static RequestDraft? ReadRogueliteDeckCallArguments(RequestPacket request)
    {
        var api = "roguelite_deck_call";
        RequestDraft? arguments = null;
        if (api == "roguelite_deck_call")
            arguments = new RequestDraft(Value: request.TryCast<RogueliteDeckCallRequest>()?.deck_no);

        return arguments;
    }
    private static RequestDraft? ReadRogueliteDeckSaveArguments(RequestPacket request)
    {
        var api = "roguelite_deck_save";
        RequestDraft? arguments = null;
        if (api == "roguelite_deck_save")
        {
            var deck = request.TryCast<RogueliteDeckSaveRequest>();
            arguments = deck == null ? null : new RequestDraft(Value: deck.deck_no,
                Ids: deck.deck_list?.ToArray().Select(id => (long)id).ToArray(), Text: deck.deck_name);
        }

        return arguments;
    }
    private static RequestDraft? ReadTeamUpdateArguments(RequestPacket request)
    {
        var api = "team_update";
        RequestDraft? arguments = null;
        if (api == "team_update")
        {
            var update = request.TryCast<TeamUpdateRequest>();
            arguments = update == null ? null : new RequestDraft(Team: new(update.number, update.name,
                update.user_unit_id_list?.ToArray(), update.use_cost, update.season_enhancement_user_unit_id_list?.ToArray(),
                update.use_cost_with_friend, MasterDB.This()?.Setting?.TEAM_MAX ?? 0));
        }

        return arguments;
    }
}
