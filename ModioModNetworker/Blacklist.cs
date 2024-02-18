namespace ModioModNetworker;

public class Blacklist
{
    public static List<string> blacklistedSteamIds = new List<string>()
    {
        "76561198843066427"
    };
    
    public static bool IsBlacklisted(string steamId)
    {
        return blacklistedSteamIds.Contains(steamId);
    }
}