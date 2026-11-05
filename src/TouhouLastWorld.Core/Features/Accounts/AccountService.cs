using System.Text.Json.Nodes;
using System.Text.Json;
namespace TouhouLastWorld;

internal sealed class AccountService(PlayerState PlayerState, SessionState SessionState, SeedMigration migration)
{

    internal void EnsureAccount()
    {
        if (SessionState.account is not null)
            return;
        if (AccountDeleted)
        {
            FinishAccountDeletion();
            PlayerState.Save(PlayerState.initialUser.DeepClone().AsObject());
            migration.Run();
        }

        var created = new SessionState.LocalAccount(Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), "LOCAL" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant());
        PlayerState.store.WriteAccount(JsonSerializer.Serialize(created));
        SessionState.account = created;
    }


    internal bool AccountDeleted => PlayerState.user["_local_account_deleted"]?.GetValue<bool>() == true;


    internal LocalReply DeleteAccount()
    {
        PlayerState.Save(new JsonObject { ["_local_account_deleted"] = true });
        SessionState.account = null;
        FinishAccountDeletion();
        return new(0, new());
    }


    internal void FinishAccountDeletion()
    {
        PlayerState.store.DeleteAccount();
    }

    internal void Migrate() => migration.Run();

}
