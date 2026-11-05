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
    private static RequestDraft? ReadGatherGoldArguments(RequestPacket request)
    {
        var api = "gather_gold";
        RequestDraft? arguments = null;
        if (api == "gather_gold")
        {
            var level = User.This()?.level;
            arguments = new RequestDraft(EntityId: level.HasValue ? MasterDB.This()?.PlayerExp?.Find(level.Value)?.gold_max : null,
                Value: level);
        }

        return arguments;
    }
    private static RequestDraft? ReadGoldBoxRenovationStartArguments(RequestPacket request)
    {
        var api = "gold_box_renovation_start";
        RequestDraft? arguments = null;
        if (api is "gold_box_renovation_start" or "gold_box_renovation_end")
        {
            var level = User.This()?.gold_box?.level;
            var next = level is > 0 and < int.MaxValue ? MasterDB.This()?.GoldBox?.Find(level.Value + 1) : null;
            arguments = new RequestDraft(EntityId: next?.level, Value: level,
                Renovation: next == null || !level.HasValue ? null : new(level.Value, next.level, next.need_player_level, next.consume_gold, next.need_time));
        }

        return arguments;
    }
    private static RequestDraft? ReadGoldBoxRenovationEndArguments(RequestPacket request)
    {
        var api = "gold_box_renovation_end";
        RequestDraft? arguments = null;
        if (api is "gold_box_renovation_start" or "gold_box_renovation_end")
        {
            var level = User.This()?.gold_box?.level;
            var next = level is > 0 and < int.MaxValue ? MasterDB.This()?.GoldBox?.Find(level.Value + 1) : null;
            arguments = new RequestDraft(EntityId: next?.level, Value: level,
                Renovation: next == null || !level.HasValue ? null : new(level.Value, next.level, next.need_player_level, next.consume_gold, next.need_time));
        }

        return arguments;
    }
    private static RequestDraft? ReadGatherPhantasmPointArguments(RequestPacket request)
    {
        var api = "gather_phantasm_point";
        RequestDraft? arguments = null;
        if (api == "gather_phantasm_point")
        {
            var level = User.This()?.level;
            arguments = new RequestDraft(EntityId: level.HasValue ? MasterDB.This()?.PlayerExp?.Find(level.Value)?.phantasm_point_max : null,
                Value: level);
        }

        return arguments;
    }
    private static RequestDraft? ReadPhantasmBoxRenovationStartArguments(RequestPacket request)
    {
        var api = "phantasm_box_renovation_start";
        RequestDraft? arguments = null;
        if (api is "phantasm_box_renovation_start" or "phantasm_box_renovation_end")
        {
            var level = User.This()?.phantasm_box?.level;
            var next = level is > 0 and < int.MaxValue ? MasterDB.This()?.PhantasmBox?.Find(level.Value + 1) : null;
            arguments = new RequestDraft(EntityId: next?.level, Value: level,
                Renovation: next == null || !level.HasValue ? null : new(level.Value, next.level, next.need_player_level, next.consume_gold, next.need_time));
        }

        return arguments;
    }
    private static RequestDraft? ReadPhantasmBoxRenovationEndArguments(RequestPacket request)
    {
        var api = "phantasm_box_renovation_end";
        RequestDraft? arguments = null;
        if (api is "phantasm_box_renovation_start" or "phantasm_box_renovation_end")
        {
            var level = User.This()?.phantasm_box?.level;
            var next = level is > 0 and < int.MaxValue ? MasterDB.This()?.PhantasmBox?.Find(level.Value + 1) : null;
            arguments = new RequestDraft(EntityId: next?.level, Value: level,
                Renovation: next == null || !level.HasValue ? null : new(level.Value, next.level, next.need_player_level, next.consume_gold, next.need_time));
        }

        return arguments;
    }
    private static RequestDraft? ReadAfkSetArguments(RequestPacket request)
    {
        var api = "afk_set";
        RequestDraft? arguments = null;
        if (api == "afk_set")
            arguments = new RequestDraft(Idle: ReadIdleSet(request.TryCast<IdleSearchSetRequest>()));

        return arguments;
    }
    private static RequestDraft? ReadAfkCompleteUpgradeArguments(RequestPacket request)
    {
        var api = "afk_complete_upgrade";
        RequestDraft? arguments = null;
        if (api == "afk_complete_upgrade")
        {
            var completion = request.TryCast<IdleSearchCompleteUpgradeRequest>();
            arguments = completion == null ? null : new RequestDraft(EntityId: completion.slot_number, Value: completion.object_type,
                Idle: ReadIdleDefinition(Plugin.Server.IdleSelection(completion.object_type, completion.slot_number)));
        }

        return arguments;
    }
    private static RequestDraft? ReadTrainingRoomShortenedFinishTimeArguments(RequestPacket request)
    {
        var api = "training_room_shortened_finish_time";
        RequestDraft? arguments = null;
        if (api == "training_room_shortened_finish_time")
        {
            var shortened = request.TryCast<TrainingRoomShortenedFinishTimeRequest>();
            var textbooks = MasterDB.This()?.Textbook?.ToList()?.ToArray();
            arguments = new RequestDraft(EntityId: shortened?.user_unit_id,
                TrainingMaterials: shortened?.material_item_list?.ToArray().Select(entry =>
                    new LocalTrainingMaterial(entry.item_id, entry.count,
                        textbooks?.FirstOrDefault(book => book.item_id == entry.item_id)?.shorten_time ?? 0)).ToArray());
        }

        return arguments;
    }
    private static RequestDraft? ReadTrainingRoomStartArguments(RequestPacket request)
    {
        var api = "training_room_start";
        RequestDraft? arguments = null;
        if (api == "training_room_start")
        {
            var training = request.TryCast<TrainingRoomStartRequest>();
            var material = MasterDB.This()?.Textbook?.ToList()?.ToArray().FirstOrDefault(entry => entry.item_id == training?.material_item_id);
            var capacity = MasterDB.This()?.TrainingRoomExpansion?.Find(Plugin.Server.TrainingExpansionRank)?.slot_count;
            arguments = training == null || material == null || capacity == null ? null
                : new RequestDraft(Training: new(training.user_unit_id, training.skill_number, training.material_item_id,
                    training.slot_number, capacity.Value, material.need_time));
        }

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomFoodDistributionArguments(RequestPacket request)
    {
        var api = "lecture_room_food_distribution";
        RequestDraft? arguments = null;
        if (api == "lecture_room_food_distribution")
        {
            var distribution = request.TryCast<LectureRoomFoodDistributionRequest>();
            var food = MasterDB.This()?.Food?.ToList()?.ToArray().FirstOrDefault(entry => entry.item_id == distribution?.item_id);
            var capacity = MasterDB.This()?.LectureRoomFoodExpansion?.Find(Plugin.Server.LectureFoodExpansionRank)?.food_limit;
            arguments = distribution == null || food == null || capacity == null ? null
                : new RequestDraft(FoodDistribution: new(distribution.item_id, food.id, distribution.num, food.value, capacity.Value, food.add_effect_time));
        }

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomFoodAdditionArguments(RequestPacket request)
    {
        var api = "lecture_room_food_addition";
        RequestDraft? arguments = null;
        if (api == "lecture_room_food_addition")
        {
            var purchase = request.TryCast<LectureRoomFoodAdditionRequest>();
            var food = MasterDB.This()?.Food?.ToList()?.ToArray().FirstOrDefault(entry => entry.item_id == purchase?.item_id);
            arguments = purchase == null || food == null ? null
                : new RequestDraft(FoodPurchase: new(purchase.item_id, purchase.num, food.consume_phantasm_point, food.consume_orb));
        }

        return arguments;
    }
    private static RequestDraft? ReadTrainingRoomCancelArguments(RequestPacket request)
    {
        var api = "training_room_cancel";
        RequestDraft? arguments = null;
        if (api == "training_room_cancel")
            arguments = new RequestDraft(Value: request.TryCast<TrainingRoomCancelRequest>()?.slot_number);

        return arguments;
    }
    private static RequestDraft? ReadDispatchCancelArguments(RequestPacket request)
    {
        var api = "dispatch_cancel";
        RequestDraft? arguments = null;
        if (api == "dispatch_cancel")
            arguments = new RequestDraft(EntityId: request.TryCast<DispatchCancelRequest>()?.user_dispatch_id);

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomUnitRemoveArguments(RequestPacket request)
    {
        var api = "lecture_room_unit_remove";
        RequestDraft? arguments = null;
        if (api == "lecture_room_unit_remove")
            arguments = new RequestDraft(EntityId: request.TryCast<LectureRoomUnitRemoveRequest>()?.user_unit_id);

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomUnitSetArguments(RequestPacket request)
    {
        var api = "lecture_room_unit_set";
        RequestDraft? arguments = null;
        if (api == "lecture_room_unit_set")
        {
            var capacity = MasterDB.This()?.LectureRoomUnitExpansion?.Find(Plugin.Server.LectureExpansionRank)?.slot_count;
            arguments = new RequestDraft(request.TryCast<LectureRoomUnitSetRequest>()?.user_unit_id, capacity);
        }

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomUnitExpansionArguments(RequestPacket request)
    {
        var api = "lecture_room_unit_expansion";
        RequestDraft? arguments = null;
        if (api == "lecture_room_unit_expansion")
        {
            var rank = Plugin.Server.LectureExpansionRank;
            var next = rank == int.MaxValue ? null : MasterDB.This()?.LectureRoomUnitExpansion?.Find(rank + 1);
            arguments = next == null ? null : new RequestDraft(rank + 1, next.consume_orb);
        }

        return arguments;
    }
    private static RequestDraft? ReadLectureRoomFoodExpansionArguments(RequestPacket request)
    {
        var api = "lecture_room_food_expansion";
        RequestDraft? arguments = null;
        if (api == "lecture_room_food_expansion")
        {
            var rank = Plugin.Server.LectureFoodExpansionRank;
            var next = rank == int.MaxValue ? null : MasterDB.This()?.LectureRoomFoodExpansion?.Find(rank + 1);
            arguments = next == null ? null : new RequestDraft(rank + 1, next.consume_orb);
        }

        return arguments;
    }
    private static RequestDraft? ReadTrainingRoomExpansionArguments(RequestPacket request)
    {
        var api = "training_room_expansion";
        RequestDraft? arguments = null;
        if (api == "training_room_expansion")
        {
            var rank = Plugin.Server.TrainingExpansionRank;
            var next = rank == int.MaxValue ? null : MasterDB.This()?.TrainingRoomExpansion?.Find(rank + 1);
            arguments = next == null ? null : new RequestDraft(rank + 1, next.consume_orb);
        }

        return arguments;
    }
}
