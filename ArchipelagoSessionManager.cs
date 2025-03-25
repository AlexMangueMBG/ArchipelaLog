using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ArchipelaLog.Models;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Discord;
using Newtonsoft.Json;

namespace ArchipelaLog
{
    public static class ArchipelagoSessionManager
    {
        private static string sessionPath = @$"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\ArchipelagoBot\Sessions.json";
        private static List<ChannelData> _sessions;
        private static readonly SemaphoreSlim s_DataLock = new(1, 1);

        private static void LoadSessions() => _sessions = JsonConvert.DeserializeObject<List<ChannelData>>(File.ReadAllText(sessionPath));
        private static void SaveSessions() => File.WriteAllText(sessionPath, JsonConvert.SerializeObject(_sessions, Formatting.Indented));

        public static async Task LoadSessionData()
        {
            try
            {
                await s_DataLock.WaitAsync();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    sessionPath = @$"{Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)}/Sessions.json";

                if (!File.Exists(sessionPath))
                {
                    if(!Directory.Exists(Path.GetDirectoryName(sessionPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(sessionPath));
                        Console.WriteLine($"Created folder {Path.GetDirectoryName(sessionPath)}");
                    }
                    File.WriteAllText(sessionPath, "[]");
                }

                LoadSessions();

                List<ChannelData> emptyChannels = new List<ChannelData>();
                
                foreach(ChannelData session in _sessions)
                {
                    session.ArchipelagoSession = new Archipelago();

                    if(!session.Users.Any())
                    {
                        emptyChannels.Add(session);
                        continue;
                    }

                    try
                    {
                        await StartSessionData(session);
                    }
                    catch(Exception ex)
                    {
                        await DiscordBot.Client.GetGuild(session.GuildId).GetTextChannel(session.ChannelId).SendMessageAndLog("Failed to login. The server might be closed. Please, run /reconnect when the server is open.", true);
                        Console.WriteLine(ex);
                        continue;
                    }
                }

                foreach(ChannelData emptySession in emptyChannels)
                {
                    _sessions.Remove(emptySession);
                    GC.Collect();
                }

                SaveSessions();
            }
            finally
            {
                s_DataLock.Release();
            }
        }

        public static async Task StartSessionData(ChannelData session)
        {
            if (session.Users.Count == 0)
                return;

            await session.ArchipelagoSession.Login(session.Hostname, session.Port, session.Users[0].SlotName);

            string description = $"**Session data**\n\tConnection: {session.Hostname}:{session.Port}";
            List<ItemUpdate> newFoundItems = new List<ItemUpdate>();

            foreach (UserData userData in session.Users)
            {
                description += $"\n\t{userData.SlotName}";
                string discordName = "";

                if (userData.IsAttached)
                {
                    discordName = (await DiscordBot.Client.GetUserAsync(userData.DiscordId)).GlobalName;
                    description += $": {discordName}";
                }

                newFoundItems.AddRange(await LoadPlayerItems(session, userData));

                //newFoundItemTexts.AddRange(newItems.Select(item => $"{item.ItemName} for {session.Users.Find(user => user.SlotName == item.Receiver).DisplayName}"));
            }

            if (newFoundItems.Count > 0)
            {
                string newItemsDescription = string.Join("\n\n",
                    newFoundItems
                    .GroupBy(item => item.Receiver)
                    .Select(player =>
                    {
                        UserData user = session.Users.Find(user => user.SlotName == player.Key);
                        string userNewFounds = "";

                        userNewFounds += $"__{user.GetDisplayName(true)}__\n";
                        userNewFounds += string.Join("\n", player.Select(item => item.ItemName));

                        return userNewFounds;
                    })
                );

                Embed sessionDataToShow = new EmbedBuilder()
                {
                    Title = "Reconnected to Archipelago.",
                    Description = description,
                }.Build();

                await DiscordBot.Client.GetGuild(session.GuildId).GetTextChannel(session.ChannelId).SendMessageAsync(embed: sessionDataToShow);

                Embed newItemsEmbed = new EmbedBuilder()
                {
                    Title = "While the bot was gone, the next important items were found:",
                    Description = newItemsDescription
                }.Build();

                await DiscordBot.Client.GetGuild(session.GuildId).GetTextChannel(session.ChannelId).SendMessageAsync(embed: newItemsEmbed);
            }
        }

        public static async Task Reconnect(ulong guildId, ulong channelId)
        {
            try
            {
                await s_DataLock.WaitAsync();

                ChannelData session = _sessions.Find(session => session.GuildId == guildId && session.ChannelId == channelId);

                if (session == null)
                    throw new Exception($"This channel has no connection information.");

                if(session.ArchipelagoSession != null)
                {
                    session.ArchipelagoSession.DettachEventHandler();
                    await session.ArchipelagoSession.Disconnect();
                }

                session.ArchipelagoSession = new Archipelago();

                try
                {
                    await StartSessionData(session);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw new Exception("Failed to login. The server might be closed. Please, run /reconnect when the server is open.");
                }
            }
            finally
            {
                s_DataLock.Release();
            }
        }

        public static async Task SetChannelConnectionInfo(ulong guildId, ulong channelId, string hostname, int port)
        {
            try
            {
                await s_DataLock.WaitAsync();

                if (_sessions == null)
                    _sessions = new List<ChannelData>();

                ChannelData relatedSession = _sessions.Find(session => session.GuildId == guildId && session.ChannelId == channelId);

                if (relatedSession == null)
                {
                    relatedSession = new ChannelData()
                    {
                        GuildId = guildId,
                        ChannelId = channelId,
                        Hostname = hostname,
                        Port = port,
                        Users = new List<UserData>(),
                        ArchipelagoSession = new Archipelago(),
                        RolesThatCanAttach = new List<string>()
                    };

                    _sessions.Add(relatedSession);
                }
                else
                {
                    relatedSession.Hostname = hostname;
                    relatedSession.Port = port;
                    relatedSession.ArchipelagoSession = new Archipelago();
                }

                SaveSessions();
            }
            finally
            {
                s_DataLock.Release();
            }
        }

        public static async Task DetachChannel(ulong guildId, ulong channelId)
        {
            try
            {
                await s_DataLock.WaitAsync();

                ChannelData session = _sessions.Find(session => session.GuildId == guildId && session.ChannelId == channelId);

                if (session == null)
                    throw new Exception($"This channel has no connection information.");

                _sessions.Remove(session);
                await session.ArchipelagoSession.Disconnect();

                SaveSessions();
            }
            finally
            {
                s_DataLock.Release();
            }
        }

        public static async Task ConnectPlayer(ulong guildId, ulong channelId, string slotName, bool notify, ulong userId)
        {
            try
            {
                await s_DataLock.WaitAsync();
                ChannelData session = _sessions.Find(session => session.GuildId == guildId && session.ChannelId == channelId);

                if (session == null)
                    throw new Exception($"This channel has no connection information. Please ask a moderator to set it up for you.");

                if (session.Users.Any(user => user.SlotName == slotName && user.IsAttached))
                {
                    IUser existingUser = await DiscordBot.Client.GetUserAsync(session.Users.Find(user => user.SlotName == slotName).DiscordId);
                    throw new Exception($"{slotName} is already tied to {existingUser.GlobalName}.");
                }

                await session.ArchipelagoSession.Login(session.Hostname, session.Port, slotName);

                UserData relatedUser = session.Users.Find(user => user.SlotName == slotName);

                if(relatedUser != null)
                {
                    relatedUser.IsAttached = true;
                    relatedUser.DiscordId = userId;
                }
                else
                {
                    UserData newUser = new UserData()
                    {
                        IsAttached = true,
                        DiscordId = userId,
                        SlotName = slotName,
                        Notify = notify,
                        ItemData = new List<ItemUpdate>()
                    };

                    newUser.ItemData.AddRange(await LoadPlayerItems(session, newUser));

                    session.Users.Add(newUser);
                }

                SaveSessions();
            }
            finally
            {
                s_DataLock.Release();
            }
        }

        public static async Task<bool> DetachPlayer(ulong guildId, ulong channelId, ulong userId)
        {
            bool isSessionStillOpen = true;

            try
            {
                await s_DataLock.WaitAsync();

                ChannelData session = _sessions.Find(session => session.GuildId == guildId && session.ChannelId == channelId);

                if (session == null)
                    throw new Exception($"This channel has no connection information.");

                if (!session.Users.Any(user => user.DiscordId == userId))
                {
                    IUser discordUser = await DiscordBot.Client.GetUserAsync(userId);
                    throw new Exception($"No slot is attached to user {discordUser.GlobalName}");
                }

                foreach (UserData user in session.Users.Where(user => user.DiscordId == userId))
                {
                    user.IsAttached = false;
                    user.DiscordId = default(ulong);
                }

                // If all players have disconnected
                if (!session.Users.Any(user => user.IsAttached))
                {
                    _sessions.Remove(session);

                    try
                    {
                        await session.ArchipelagoSession.Disconnect();
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine("Failed to disconnect session");
                    }

                    isSessionStillOpen = false;
                    GC.Collect();
                }

                SaveSessions();
            }
            finally
            {
                s_DataLock.Release();
            }

            return isSessionStillOpen;
        }

        public static async Task NotifyPlayers(ItemSendLogMessage msg, Archipelago sender)
        {
            try
            {
                await s_DataLock.WaitAsync();
                ChannelData session = _sessions.Find(session => session.ArchipelagoSession.Guid == sender.Guid);

                if(session == null)
                {
                    sender.DettachEventHandler();
                }
                else
                {
                    bool isSenderAndReceiver = msg.Sender.Name == msg.Receiver.Name;
                    bool importantItem = msg.Item.Flags.HasFlag(ItemFlags.Advancement) || msg.Item.Flags.HasFlag(ItemFlags.NeverExclude);

                    string formattedItemName = importantItem ? $"**{msg.Item.ItemName}**" : msg.Item.ItemName;

                    // Original unmodified message
                    string discordMessage = $"$Sender found $Receiver {formattedItemName} at {msg.Item.LocationDisplayName}";

                    UserData senderData = session.Users.Find(user => user.SlotName == msg.Sender.Name);
                    UserData receiverData = session.Users.Find(user => user.SlotName == msg.Receiver.Name);

                    IUser senderDiscordUser = null;
                    IUser receiverDiscordUser = null;

                    if (senderData != null && senderData.IsAttached)
                        senderDiscordUser = await DiscordBot.Client.GetUserAsync(senderData.DiscordId);

                    if (receiverData != null && receiverData.IsAttached)
                        receiverDiscordUser = await DiscordBot.Client.GetUserAsync(receiverData.DiscordId);

                    Dictionary<string, string> replacements = new Dictionary<string, string>(){
                    { "$Sender", msg.Sender.Name },
                    { "$Receiver", $"{msg.Receiver.Name}'s" }
                    };

                    if (isSenderAndReceiver)
                    {
                        replacements["$Receiver"] = "their";

                        if (senderData != null && senderDiscordUser != null)
                        {
                            if (senderData.Notify && importantItem)
                                replacements["$Sender"] = $"{senderDiscordUser.Mention}";
                            else
                                replacements["$Sender"] = $"{senderDiscordUser.GlobalName}";
                        }
                    }
                    else
                    {
                        if (senderDiscordUser != null)
                            replacements["$Sender"] = senderDiscordUser.GlobalName;

                        if (receiverData != null && receiverDiscordUser != null)
                        {
                            if (receiverData.Notify && importantItem)
                                replacements["$Receiver"] = $"{receiverDiscordUser.Mention}'s";
                            else
                                replacements["$Receiver"] = $"{receiverDiscordUser.GlobalName}'s";
                        }
                    }

                    // Apply replacements
                    foreach (KeyValuePair<string, string> replacement in replacements)
                        discordMessage = discordMessage.Replace(replacement.Key, replacement.Value);

                    await DiscordBot.Client.GetGuild(session.GuildId).GetTextChannel(session.ChannelId).SendMessageAndLog(discordMessage);

                    if (senderData == null)
                    {
                        senderData = new UserData()
                        {
                            IsAttached = false,
                            DiscordId = default(ulong),
                            SlotName = msg.Sender.Name,
                            Notify = false,
                            ItemData = new List<ItemUpdate>()
                        };

                        session.Users.Add(senderData);
                    }

                    if (receiverData == null)
                    {
                        receiverData = new UserData()
                        {
                            IsAttached = false,
                            DiscordId = default(ulong),
                            SlotName = msg.Receiver.Name,
                            Notify = false,
                            ItemData = new List<ItemUpdate>()
                        };

                        session.Users.Add(receiverData);
                    }

                    receiverData.ItemData.Add(new ItemUpdate()
                    {
                        Sender = senderData.SlotName,
                        Receiver = receiverData.SlotName,
                        ItemName = msg.Item.ItemName,
                        ItemLocation = msg.Item.LocationDisplayName,
                        ItemFlags = msg.Item.Flags
                    });

                    SaveSessions();
                }
            }
            finally
            {
                s_DataLock.Release();
            }
        }

        private static async Task<IEnumerable<ItemUpdate>> LoadPlayerItems(ChannelData session, UserData userData, bool filterNonImportant = false)
        {
            IEnumerable<ScoutedItemInfo> checkedItems = await Archipelago.Received(session.Hostname, session.Port, userData.SlotName);
            List<ItemUpdate> newItems = new List<ItemUpdate>();

            foreach(var player in checkedItems.GroupBy(item => item.Player))
            {
                UserData user = session.Users.Find(user => user.SlotName == player.Key.Name);

                if(user == null)
                {
                    user = new UserData()
                    {
                        IsAttached = false,
                        DiscordId = default(ulong),
                        SlotName = player.Key.Name,
                        Notify = false,
                        ItemData = new List<ItemUpdate>()
                    };

                    session.Users.Add(user);
                }

                List<ItemUpdate> newUserItems = player.Where(item => !user.ItemData.Select(uItem => uItem.ItemName).Contains(item.ItemName)).Select(item => new ItemUpdate()
                {
                    ItemName = item.ItemName,
                    Sender = userData.SlotName,
                    Receiver = item.Player.Name,
                    ItemLocation = item.LocationName,
                    ItemFlags = item.Flags
                }).ToList();

                user.ItemData.AddRange(newUserItems);
                newItems.AddRange(newUserItems.Where(item => !filterNonImportant || item.ItemFlags.HasFlag(ItemFlags.Advancement) || item.ItemFlags.HasFlag(ItemFlags.NeverExclude)));
            }

            return newItems;
        }
    }
}
