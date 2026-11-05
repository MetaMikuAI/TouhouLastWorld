namespace TouhouLastWorld;

public sealed record SetEquipmentCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalEquipmentEntry[]? Equipment = null);
