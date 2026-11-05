namespace TouhouLastWorld;

public sealed record ReceiveMailCommand(
    global::System.Int64[]? Ids = null,
    global::TouhouLastWorld.LocalMailRules? MailRules = null);
