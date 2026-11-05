namespace TouhouLastWorld;

public interface IStateStore
{
    string? ReadUser();
    string? ReadAccount();
    void WriteUser(string json);
    void WriteAccount(string json);
    void BackupBeforeMigration();
    void DeleteAccount();
}
