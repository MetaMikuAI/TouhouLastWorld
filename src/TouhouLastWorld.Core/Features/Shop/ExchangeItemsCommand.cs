namespace TouhouLastWorld;

public sealed record ExchangeItemsCommand(
    global::System.Int64? EntityId = null,
    global::TouhouLastWorld.LocalItemExchange? ItemExchange = null,
    global::System.Int32? Value = null);
