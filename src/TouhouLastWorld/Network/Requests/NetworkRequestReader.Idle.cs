using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static LocalIdleSet? ReadIdleSet(IdleSearchSetRequest? request)
    {
        if (request == null) return null;
        var result = new LocalIdleSet(request.user_object_id, request.object_type, request.slot_number, request.content, request.is_cancel);
        return ReadIdleDefinition(result);
    }

    private static LocalIdleSet? ReadIdleDefinition(LocalIdleSet? request)
    {
        if (request is null) return null;
        var result = request;
        if (request.Cancel != 0) return result;
        var master = MasterDB.This();
        var snapshot = IdleSearchTop.afkResponse;
        var content = request.Content == 7 ? 6 : request.Content;
        var duration = master?.AfkGrowth?.Find(content, Plugin.Server.IdleStage(content))?.reward_value ?? 0;
        var capacity = request.ObjectType == 9 ? snapshot?.unitSlotCount ?? 0 : snapshot?.pictSlotCount ?? 0;
        if (request.ObjectType == 9)
        {
            var unit = User.This()?.unit?.Find(request.ObjectId);
            if (unit == null) return result;
            if (request.Content == 4)
                return result with
                {
                    Capacity = capacity,
                    GrowthField = "rare",
                    CurrentValue = unit.rare,
                    Eligible = master?.UnitRareAwakenGrowth?.Find(unit.rare + 1) != null,
                    Seconds = (long)duration * (master?.AfkGrowthTimeCoef?.Find(4, unit.rare)?.coef ?? 0)
                };
            if (request.Content == 2)
                return result with
                {
                    Capacity = capacity,
                    GrowthField = "destiny_value",
                    CurrentValue = unit.destiny_value,
                    Eligible = unit.is_limitbreak_max && unit.destiny_value < (master?.Setting?.AFK_DESTINY_VALUE_MAX ?? 0),
                    Seconds = duration
                };
        }
        if (request.ObjectType == 10)
        {
            var picture = User.This()?.picture?.Find(request.ObjectId);
            if (picture == null) return result;
            if (request.Content == 7)
                return result with
                {
                    Capacity = capacity,
                    GrowthField = "spec_actualize",
                    CurrentValue = picture.spec_actualize,
                    Eligible = picture.spec_actualize < picture.spec_actualize_limit,
                    Seconds = (long)duration * (master?.AfkGrowthTimeCoef?.Find(7, picture.spec_actualize)?.coef ?? 0)
                };
            if (request.Content == 3)
                return result with
                {
                    Capacity = capacity,
                    GrowthField = "potential_limitbreak",
                    CurrentValue = picture.potential_limitbreak,
                    Eligible = picture.potential_limitbreak < picture.potential_limitbreak_limit,
                    Seconds = duration
                };
        }
        return result;
    }
}
