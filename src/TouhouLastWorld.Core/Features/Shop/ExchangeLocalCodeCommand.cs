namespace TouhouLastWorld;

public sealed record ExchangeLocalCodeCommand(
    global::TouhouLastWorld.LocalCodeExchange? CodeExchange = null,
    global::TouhouLastWorld.LocalMailRules? MailRules = null);
