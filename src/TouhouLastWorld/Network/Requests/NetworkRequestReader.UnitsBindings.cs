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
    private static RequestDraft? ReadUnitRecycleArguments(RequestPacket request)
    {
        var api = "unit_recycle";
        RequestDraft? arguments = null;
        if (api == "unit_recycle") arguments = ReadUnitRecycle(request.TryCast<UnitRecycleRequest>());

        return arguments;
    }
    private static RequestDraft? ReadUnitReincarnationReleaseArguments(RequestPacket request)
    {
        var api = "unit_reincarnation_release";
        RequestDraft? arguments = null;
        if (api == "unit_reincarnation_release") arguments = ReadAutomaticRelease(request.TryCast<ReincarnationReleaseRequest>());

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomUnitTrustUpArguments(RequestPacket request)
    {
        var api = "lecture_room_unit_trust_up";
        RequestDraft? arguments = null;
        if (api == "lecture_room_unit_trust_up")
        {
            var change = request.TryCast<LectureRoomUnitTrustUpRequest>();
            arguments = new RequestDraft(change?.user_unit_id, MasterDB.This()?.UnitTrustGrowth?.Last()?.trust_upper_limit);
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitRankPromoteItemSetArguments(RequestPacket request)
    {
        var api = "unit_rank_promote_item_set";
        RequestDraft? arguments = null;
        if (api == "unit_rank_promote_item_set")
            arguments = ReadRankMaterial(request.TryCast<UnitRankPromoteItemSetRequest>());

        return arguments;
    }
    private static RequestDraft? ReadUnitRankPromoteArguments(RequestPacket request)
    {
        var api = "unit_rank_promote";
        RequestDraft? arguments = null;
        if (api == "unit_rank_promote")
            arguments = ReadRankPromotion(request.TryCast<UnitRankPromoteRequest>());

        return arguments;
    }
    private static RequestDraft? ReadUnitSpecActualizeArguments(RequestPacket request)
    {
        var api = "unit_spec_actualize";
        RequestDraft? arguments = null;
        if (api == "unit_spec_actualize")
            arguments = ReadStrengthening(request.TryCast<UnitSpecActualizeRequest>());

        return arguments;
    }
    private static RequestDraft? ReadUnitExtraSpecActualizeArguments(RequestPacket request)
    {
        var api = "unit_extra_spec_actualize";
        RequestDraft? arguments = null;
        if (api == "unit_extra_spec_actualize")
            arguments = ReadExtraStrengthening(request.TryCast<UnitExtraSpecActualizeRequest>());

        return arguments;
    }
    private static RequestDraft? ReadLimitbreakStrongFragrantwoodArguments(RequestPacket request)
    {
        var api = "limitbreak_strong_fragrantwood";
        RequestDraft? arguments = null;
        if (api == "limitbreak_strong_fragrantwood")
            arguments = ReadStatUnlock(request.TryCast<UnitLimitbreakStrongFragrantWoodRequest>());

        return arguments;
    }
    private static RequestDraft? ReadUnitDestinyValueUpArguments(RequestPacket request)
    {
        var api = "unit_destiny_value_up";
        RequestDraft? arguments = null;
        if (api == "unit_destiny_value_up")
        {
            var change = request.TryCast<UnitDestinyValueUpRequest>();
            var unit = change == null ? null : User.This()?.unit?.Find(change.user_unit_id);
            var master = MasterDB.This();
            var definition = unit == null ? null : master?.Unit?.Find(unit.unit_id);
            var setting = master?.Setting;
            arguments = new RequestDraft(change?.user_unit_id, Ids: change?.material_user_unit_id_list?.ToArray());
            if (change != null && unit != null && definition != null && setting != null)
                arguments = arguments with
                {
                    DestinyGrowth = new(unit.unit_id, unit.destiny_value, setting.DESTINY_VALUE_MAX,
                [
                    new(definition.limitbreak_item_id, change.material_item_num, setting.DESTINY_VALUE_ADD_FOR_PAPER_DOLL),
                    new(setting.DESTINY_VALUE_MATERIAL_ID, change.material_exclusive_item_num, 1),
                    new(definition.destiny_value_up_item_id, change.unit_exclusive_item_num, setting.DESTINY_VALUE_ADD_UNIT_EXCLUSIVE_ITEM)
                ])
                };
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitLevelLimitbreakArguments(RequestPacket request)
    {
        var api = "unit_level_limitbreak";
        RequestDraft? arguments = null;
        if (api == "unit_level_limitbreak")
        {
            var change = request.TryCast<UnitLevelLimitbreakRequest>();
            var unit = change == null ? null : User.This()?.unit?.Find(change.user_unit_id);
            var master = MasterDB.This();
            var next = unit == null || unit.is_limitbreak_max || unit.level_limitbreak == int.MaxValue
                ? null : master?.UnitLevelLimitbreakGrowth?.Find(unit.level_limitbreak + 1);
            var definition = unit == null ? null : master?.Unit?.Find(unit.unit_id);
            var setting = master?.Setting;
            arguments = new RequestDraft(change?.user_unit_id, change?.material_item_num, change?.material_user_unit_id_list?.ToArray(),
                LimitBreak: unit == null || next == null || definition == null || setting == null ? null
                    : new(unit.unit_id, unit.level_limitbreak, next.level_limitbreak, next.level_lower_limit,
                        next.consume_gold, next.consume_same_unit_count, definition.limitbreak_item_id,
                        setting.DESTINY_VALUE_ADD_FOR_PAPER_DOLL, setting.DESTINY_VALUE_MAX));
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitRareAwakenArguments(RequestPacket request)
    {
        var api = "unit_rare_awaken";
        RequestDraft? arguments = null;
        if (api == "unit_rare_awaken")
        {
            var awakening = request.TryCast<UnitRareAwakenRequest>();
            var unit = awakening == null ? null : User.This()?.unit?.Find(awakening.user_unit_id);
            var next = unit == null || unit.is_rare_max || unit.rare == int.MaxValue
                ? null : MasterDB.This()?.UnitRareAwakenGrowth?.Find(unit.rare + 1);
            arguments = new RequestDraft(awakening?.user_unit_id,
                Awakening: unit == null || next == null ? null : new(unit.rare, next.rare, next.consume_piece));
        }

        return arguments;
    }
    private static RequestDraft? ReadAllocatePointsArguments(RequestPacket request)
    {
        var api = "allocate_points";
        RequestDraft? arguments = null;
        if (api == "allocate_points")
        {
            var allocation = request.TryCast<UnitAllocatePointsRequest>();
            var rules = MasterDB.This()?.PointAllocation?.ToList()?.ToArray()
                .Select(rule => new LocalPointRule(rule.type, rule.step, rule.need_point, rule.required_total_used_point)).ToArray();
            arguments = allocation == null ? null : new RequestDraft(PointAllocation: new(allocation.user_unit_id,
                allocation.allocate_dict_list?.ToArray().Select(entry => new LocalPointTarget(entry.type, entry.step)).ToArray(), rules));
        }

        return arguments;
    }
    private static RequestDraft? ReadResetPointAllocationArguments(RequestPacket request)
    {
        var api = "reset_point_allocation";
        RequestDraft? arguments = null;
        if (api == "reset_point_allocation")
        {
            var reset = request.TryCast<UnitResetPointAllocationRequest>();
            var setting = MasterDB.This()?.Setting;
            arguments = reset == null || setting == null ? null : new RequestDraft(PointReset:
                new(reset.user_unit_id, setting.PA_RESET_CONSUME_OBJECT_VALUE, setting.PA_RESET_FREE_FLAG >= 1));
        }

        return arguments;
    }
}
