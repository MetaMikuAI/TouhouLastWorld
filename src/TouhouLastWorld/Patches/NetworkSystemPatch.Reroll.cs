namespace TouhouLastWorld.Patches;

public partial class NetworkSystemPatch
{
    private static LocalArguments? ReadReroll(RogueLiteRerollRequest? request)
    {
        var definitions = MasterDB.This()?.AbilitySeal?.ToList()?.ToArray();
        var offered = RogueLiteTop.drawnSeals?.ToArray();
        if (request == null || definitions == null || offered == null) return null;
        return new(Reroll: new(request.selected_seal, offered.Select(id => id).ToArray(),
            RogueLiteTop.remainingDraws, RogueLiteTop.remainingRerolls, definitions.Select(row => row.id).ToArray()));
    }
}
