namespace TouhouLastWorld;

public sealed record BuyPassGoodsCommand(
    global::TouhouLastWorld.LocalPassGoods? PassGoods = null,
    global::System.Int32? Value = null);
