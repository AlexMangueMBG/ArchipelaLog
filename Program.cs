using ArchipelaLog;

public class Program
{
    public static async Task Main()
    {
        await DiscordBot.Setup();
        await ArchipelagoSessionManager.LoadSessionData();

        await Task.Delay(-1);
    }
}