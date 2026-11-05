namespace TouhouLastWorld;

public sealed record SendContactRequestCommand(
    global::System.Int64? EntityId = null,
    global::System.String? Text = null,
    global::System.Int32? Value = null);
