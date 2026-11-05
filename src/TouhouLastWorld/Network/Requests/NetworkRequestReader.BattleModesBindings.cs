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
    private static RequestDraft? ReadTowerUnitRecoveryArguments(RequestPacket request)
    {
        var api = "tower_unit_recovery";
        RequestDraft? arguments = null;
        if (api == "tower_unit_recovery") arguments = new(Text: request.TryCast<TowerUnitRecoveryRequest>()?.tower_unit_info);

        return arguments;
    }
    private static RequestDraft? ReadMiniGameTopArguments(RequestPacket request)
    {
        var api = "mini_game_top";
        RequestDraft? arguments = null;
        if (api == "mini_game_top") arguments = new(Ids: MasterDB.This()?.MiniGame?.GetAll()?.ToArray().Select(row => (long)row.id).ToArray());

        return arguments;
    }
    private static RequestDraft? ReadMultipleSpiritsWeeklyTopArguments(RequestPacket request)
    {
        var api = "multiple_spirits_weekly_top";
        RequestDraft? arguments = null;
        if (api == "multiple_spirits_weekly_top") arguments = ReadVersusOverview();

        return arguments;
    }
    private static RequestDraft? ReadMultipleSpiritsWeeklyBattleStartArguments(RequestPacket request)
    {
        var api = "multiple_spirits_weekly_battle_start";
        RequestDraft? arguments = null;
        if (api == "multiple_spirits_weekly_battle_start") arguments = ReadVersusStart(request.TryCast<VersusBattleStartRequest>());

        return arguments;
    }
    private static RequestDraft? ReadRogueliteRerollArguments(RequestPacket request)
    {
        var api = "roguelite_reroll";
        RequestDraft? arguments = null;
        if (api == "roguelite_reroll") arguments = ReadReroll(request.TryCast<RogueLiteRerollRequest>());

        return arguments;
    }
    private static RequestDraft? ReadBatchSearchArguments(RequestPacket request)
    {
        var api = "batch_search";
        RequestDraft? arguments = null;
        if (api == "batch_search") arguments = ReadBatchSearch(request.TryCast<BatchSearchRequest>());

        return arguments;
    }
    private static RequestDraft? ReadMultipleSpiritsWeeklyBattleEndArguments(RequestPacket request)
    {
        var api = "multiple_spirits_weekly_battle_end";
        RequestDraft? arguments = null;
        if (api == "multiple_spirits_weekly_battle_end") arguments = ReadVersusEnd(request.TryCast<VersusBattleEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadArenaBattleEndArguments(RequestPacket request)
    {
        var api = "arena_battle_end";
        RequestDraft? arguments = null;
        if (api == "arena_battle_end") arguments = ReadArenaEnd(request.TryCast<ArenaBattleEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadArenaBattleStartArguments(RequestPacket request)
    {
        var api = "arena_battle_start";
        RequestDraft? arguments = null;
        if (api == "arena_battle_start") arguments = ReadArenaStart(request.TryCast<ArenaBattleStartRequest>());

        return arguments;
    }
    private static RequestDraft? ReadTowerBattleEndArguments(RequestPacket request)
    {
        var api = "tower_battle_end";
        RequestDraft? arguments = null;
        if (api == "tower_battle_end") arguments = ReadTowerEnd(request.TryCast<TowerBattleEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadTowerBattleStartArguments(RequestPacket request)
    {
        var api = "tower_battle_start";
        RequestDraft? arguments = null;
        if (api == "tower_battle_start")
        {
            var change = request.TryCast<TowerBattleStartRequest>();
            var master = MasterDB.This();
            var row = change == null ? null : master?.Tower?.Find(change.tower_master_id);
            if (row != null && row.battle_id == change!.battle_id)
                arguments = new(TowerStart: new(row.id, row.battle_id, row.floor, change.team_number, master!.Tower.ToList().ToArray().Max(entry => entry.floor)));
        }

        return arguments;
    }
    private static RequestDraft? ReadSeriesBattleEndArguments(RequestPacket request)
    {
        var api = "series_battle_end";
        RequestDraft? arguments = null;
        if (api == "series_battle_end") arguments = ReadSeriesEnd(request.TryCast<SeriesBattleEndRequest>());

        return arguments;
    }
    private static RequestDraft? ReadSeriesBattleStartArguments(RequestPacket request)
    {
        var api = "series_battle_start";
        RequestDraft? arguments = null;
        if (api == "series_battle_start") arguments = ReadSeriesStart(request.TryCast<SeriesBattleStartRequest>());

        return arguments;
    }
    private static RequestDraft? ReadWeeklyArenaBattleScoreResetArguments(RequestPacket request)
    {
        var api = "weekly_arena_battle_score_reset";
        RequestDraft? arguments = null;
        if (api == "weekly_arena_battle_score_reset")
            arguments = new RequestDraft(Value: request.TryCast<WeeklyArenaBattleScoreResetRequest>()?.battle_number);

        return arguments;
    }
    private static RequestDraft? ReadRhythmGameStartArguments(RequestPacket request)
    {
        var api = "rhythm_game_start";
        RequestDraft? arguments = null;
        if (api == "rhythm_game_start")
        {
            var id = request.TryCast<RhythmGameStartRequest>()?.rhythm_game_music_master_id;
            arguments = new RequestDraft(id, id is > 0 && MasterDB.This()?.RhythmGameMusic?.Find(id.Value) != null ? 1 : 0);
        }

        return arguments;
    }
    private static RequestDraft? ReadRhythmGameEndArguments(RequestPacket request)
    {
        var api = "rhythm_game_end";
        RequestDraft? arguments = null;
        if (api == "rhythm_game_end")
        {
            var result = request.TryCast<RhythmGameEndRequest>();
            arguments = result == null ? null : new RequestDraft(Rhythm: new(result.rhythm_game_music_master_id,
                result.difficulty, result.score, result.rate, result.rank));
        }

        return arguments;
    }
}
