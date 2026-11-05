using static TouhouLastWorld.Patches.ActivePanels;
namespace TouhouLastWorld.Patches;

public partial class NetworkRequestReader
{
    private static RequestDraft? ReadArenaStart(ArenaBattleStartRequest? request)
    {
        var master = MasterDB.This();
        if (request == null || master?.CustomBattle?.Find(request.custom_battle_id) == null) return null;
        return new(ArenaStart: new(request.category, request.custom_battle_id, request.enemy_level, request.difficulty,
            request.battle_number, request.team_number?.ToArray().Select(number => number).ToArray() ?? []));
    }
}
