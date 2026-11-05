namespace TouhouLastWorld;

public sealed class JsonStateStore : IStateStore
{
    private readonly string userPath;
    private readonly string accountPath;

    public JsonStateStore(string directory)
    {
        Directory.CreateDirectory(directory);
        userPath = Path.Combine(directory, "user.json");
        accountPath = Path.Combine(directory, "account.json");
    }

    public string? ReadUser() => File.Exists(userPath) ? File.ReadAllText(userPath) : null;
    public string? ReadAccount() => File.Exists(accountPath) ? File.ReadAllText(accountPath) : null;
    public void WriteUser(string json) => Write(userPath, json, true);
    public void WriteAccount(string json) => Write(accountPath, json, false);

    private static void Write(string path, string json, bool overwrite)
    {
        File.WriteAllText(path + ".tmp", json);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(path + ".tmp", path, overwrite);
                return;
            }
            catch (Exception error) when (OperatingSystem.IsWindows() && attempt < 4
                && (error is UnauthorizedAccessException || error is IOException && (error.HResult & 0xffff) is 32 or 33))
            {
                Thread.Sleep(25 << attempt);
            }
        }
    }

    public void BackupBeforeMigration()
    {
        if (!File.Exists(userPath + ".before-seed-fix"))
            File.Copy(userPath, userPath + ".before-seed-fix");
    }

    public void DeleteAccount()
    {
        File.Delete(accountPath);
        File.Delete(accountPath + ".tmp");
        File.Delete(userPath + ".before-seed-fix");
    }
}
