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
    private static RequestDraft? ReadGachaPointExchangeArguments(RequestPacket request)
    {
        var api = "gacha_point_exchange";
        RequestDraft? arguments = null;
        if (api == "gacha_point_exchange") arguments = ReadPointExchange(request.TryCast<GachaPointExchangeRequest>());

        return arguments;
    }
    private static RequestDraft? ReadRetryGachaFixArguments(RequestPacket request)
    {
        var api = "retry_gacha_fix";
        RequestDraft? arguments = null;
        if (api == "retry_gacha_fix")
        {
            var change = request.TryCast<GachaRetryFixRequest>();
            if (change != null)
            {
                arguments = new RequestDraft(Value: change.gacha_id);
                var master = MasterDB.This();
                var current = User.This();
                var row = master?.Gacha?.Find(change.gacha_id);
                var selected = current?.FindRetryGachaData(change.gacha_id);
                var capacity = current?.unit == null ? null : master?.UnitExpansion?.Find(current.unit.expansion_rank);
                var time = GameSystem.This()?.Time;
                if (row != null && selected?.object_type == 9 && master?.Unit?.Find(selected.object_id) != null && capacity != null && time != null)
                    arguments = arguments with
                    {
                        RetryFix = new(row.id, selected.object_id, capacity.unit_count,
                        string.IsNullOrWhiteSpace(row.end_at) || time.IsActiveDate(row.start_at, row.end_at))
                    };
            }
        }

        return arguments;
    }
    private static RequestDraft? ReadFirstDrawSaveArguments(RequestPacket request)
    {
        var api = "first_draw_save";
        RequestDraft? arguments = null;
        if (api == "first_draw_save" && request.TryCast<FirstGachaSaveRequest>() is not null)
        {
            var master = MasterDB.This();
            var current = User.This();
            var unitCapacity = current?.unit == null ? null : master?.UnitExpansion?.Find(current.unit.expansion_rank);
            var pictureCapacity = current?.picture == null ? null : master?.PictureExpansion?.Find(current.picture.expansion_rank);
            var ids = Plugin.Server.PendingIntroPictures;
            if (unitCapacity != null && pictureCapacity != null && ids.All(id => master?.Picture?.Find(id) != null))
                arguments = new RequestDraft(IntroGrant: new(unitCapacity.unit_count, pictureCapacity.picture_count,
                    ids.Select(id =>
                    {
                        var picture = UserData.UserPicture.CreateUserPicture(id);
                        return new LocalIntroPicture(id, picture.parameter1_value, picture.parameter2_value,
                            picture.picture_characteristic_group_master_id);
                    }).ToArray()));
        }

        return arguments;
    }
    private static RequestDraft? ReadFirstDrawDataArguments(RequestPacket request)
    {
        var api = "first_draw_data";
        RequestDraft? arguments = null;
        if (api == "first_draw_data")
        {
            var change = request.TryCast<FirstGachaDrawRequest>();
            var master = MasterDB.This();
            var units = master?.Unit?.ToList()?.ToArray();
            var pictures = master?.Picture?.ToList()?.ToArray();
            arguments = new RequestDraft(Text: change?.public_id, IntroDefinitions: units == null || pictures == null ? null
                : new(units.Select(row => row.id).ToArray(), pictures.Select(row => row.id).ToArray()));
        }

        return arguments;
    }
}
