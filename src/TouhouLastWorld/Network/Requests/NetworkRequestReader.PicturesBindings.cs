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
    private static RequestDraft? ReadPictureRecycleArguments(RequestPacket request)
    {
        var api = "picture_recycle";
        RequestDraft? arguments = null;
        if (api == "picture_recycle")
            arguments = ReadPictureRecycle(request.TryCast<PictureRecycleRequest>());

        return arguments;
    }
    private static RequestDraft? ReadPicturePotentialLimitbreakArguments(RequestPacket request)
    {
        var api = "picture_potential_limitbreak";
        RequestDraft? arguments = null;
        if (api == "picture_potential_limitbreak")
            arguments = ReadPictureLimitBreak(request.TryCast<PicturePotentialLimitbreakRequest>());

        return arguments;
    }
    private static RequestDraft? ReadPictureSpecActualizeArguments(RequestPacket request)
    {
        var api = "picture_spec_actualize";
        RequestDraft? arguments = null;
        if (api == "picture_spec_actualize")
        {
            var change = request.TryCast<PictureSpecActualizeRequest>();
            var picture = change == null ? null : User.This()?.picture?.Find(change.user_picture_id);
            arguments = new RequestDraft(change?.user_picture_id);
            if (picture != null && picture.spec_actualize >= 0 && picture.spec_actualize < picture.spec_actualize_limit)
            {
                var status = new PictureStatus(picture.picture_id);
                var next = MasterDB.This()?.PictureSpecActualize?.Find(status.GetPictureType(), status.GetRare(), picture.spec_actualize + 1);
                if (next != null) arguments = arguments with
                {
                    PictureGrowth = new(picture.picture_id, picture.spec_actualize,
                    next.spec_actualize, picture.spec_actualize_limit, next.item_id, next.item_value, next.consume_gold)
                };
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadPictureSpecActualizeChemicalsArguments(RequestPacket request)
    {
        var api = "picture_spec_actualize_chemicals";
        RequestDraft? arguments = null;
        if (api == "picture_spec_actualize_chemicals")
        {
            var change = request.TryCast<PictureSpecActualizeChemicalRequest>();
            var picture = change == null ? null : User.This()?.picture?.Find(change.user_picture_id);
            arguments = new RequestDraft(change?.user_picture_id);
            if (picture != null && picture.spec_actualize >= 0 && picture.spec_actualize < picture.spec_actualize_limit)
            {
                var status = new PictureStatus(picture.picture_id);
                var master = MasterDB.This();
                var next = master?.PictureEnhanceChemical?.Find(status.GetPictureType(), status.GetRare(), picture.spec_actualize + 1);
                var growth = master?.PictureSpecActualize?.Find(status.GetPictureType(), status.GetRare(), picture.spec_actualize + 1);
                if (next != null && growth != null) arguments = arguments with
                {
                    PictureGrowth = new(picture.picture_id, picture.spec_actualize,
                    next.spec_actualize, picture.spec_actualize_limit, next.item_id, next.item_value, 0)
                };
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitEquipPictureResetArguments(RequestPacket request)
    {
        var api = "unit_equip_picture_reset";
        RequestDraft? arguments = null;
        if (api == "unit_equip_picture_reset")
            arguments = new RequestDraft(EntityId: request.TryCast<UnitEquipPictureResetRequest>()?.user_unit_id);

        return arguments;
    }
    private static RequestDraft? ReadUnitEquipCostumeArguments(RequestPacket request)
    {
        var api = "unit_equip_costume";
        RequestDraft? arguments = null;
        if (api == "unit_equip_costume")
        {
            var update = request.TryCast<UnitEquipCostumeRequest>();
            arguments = new RequestDraft(Costumes: update?.costume_list?.ToArray().Select(entry =>
            {
                if (entry == null) return new LocalCostumeEntry(0, 0, 0, false);
                var definition = MasterDB.This()?.Costume?.Find(entry.costume_id);
                var unit = definition == null ? null : MasterDB.This()?.Unit?.Find(definition.unit_id);
                return new LocalCostumeEntry(entry.user_unit_id, entry.costume_id, definition?.unit_id ?? 0,
                    unit?.default_costume_id == entry.costume_id);
            }).ToArray());
        }

        return arguments;
    }
    private static RequestDraft? ReadUnitEquipPictureArguments(RequestPacket request)
    {
        var api = "unit_equip_picture";
        RequestDraft? arguments = null;
        if (api == "unit_equip_picture")
        {
            var update = request.TryCast<UnitEquipPictureRequest>();
            arguments = update == null ? null : new RequestDraft(update.user_unit_id,
                Equipment: update.picture_list?.ToArray().Select(entry => entry == null
                    ? new LocalEquipmentEntry(0, 0) : new LocalEquipmentEntry(entry.spellcard_number, entry.user_picture_id)).ToArray());
        }

        return arguments;
    }
}
