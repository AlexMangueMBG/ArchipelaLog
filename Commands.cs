using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace ArchipelaLog
{
    [AttributeUsage(AttributeTargets.Method)]
    public class CommandAttribute : Attribute
    {
        [Flags]
        public enum DiscordRoles
        {
            None,
            Moderator
        }

        public string Name;
        public string Description;
        public DiscordRoles Role;

        public CommandAttribute(string name, string description, DiscordRoles roles = DiscordRoles.None)
        {
            this.Name = name;
            this.Description = description;
            this.Role = roles;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CommandParameterAttribute : Attribute
    {
        public string Name;
        public string Description;
        public ApplicationCommandOptionType OptionType;
        public bool Required;

        public CommandParameterAttribute(string name, string description, ApplicationCommandOptionType optionType, bool required)
        {
            this.Name = name;
            this.Description = description;
            this.OptionType = optionType;
            Required = required;
        }
    }

    public static class Commands
    {
        [Command("setup", "Sets the connection data of the archipelago server.", CommandAttribute.DiscordRoles.Moderator)]
        [CommandParameter("hostname", "IP of the server you want to connect to", ApplicationCommandOptionType.String, true)]
        [CommandParameter("port", "Port of the server you want to connect to", ApplicationCommandOptionType.Integer, true)]
        public static async Task Setup(string hostname, long port, SocketSlashCommand command = null)
        {
            if (!command.GuildId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a discord server");
                return;
            }

            if(!command.ChannelId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a server channel.");
                return;
            }

            await ArchipelagoSessionManager.SetChannelConnectionInfo(command.GuildId.Value, command.ChannelId.Value, hostname, (int)port);
            await command.RespondAndLog($"Successfully setup {command.Channel.Name} connection information.");
        }

        [Command("disconnect", "Disconnects this channel from Archipelago. This channel will no longer receive updates.", CommandAttribute.DiscordRoles.Moderator)]
        public static async Task Disconnect(SocketSlashCommand command = null)
        {
            if (!command.GuildId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a discord server");
                return;
            }

            if (!command.ChannelId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a server channel.");
                return;
            }

            await ArchipelagoSessionManager.DetachChannel(command.GuildId.Value, command.ChannelId.Value);
            await command.RespondAndLog($"Successfully disconnected this channel from Archipelago.");
        }

        [Command("reconnect", "Reconnects to the archipelago session.")]
        public static async Task Reconnect(SocketSlashCommand command = null)
        {
            if(!command.GuildId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a discord server");
                return;
            }

            if(!command.ChannelId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a server channel.");
                return;
            }

            await ArchipelagoSessionManager.Reconnect(command.GuildId.Value, command.ChannelId.Value);
        }

        [Command("slot", "Ties the user to the archipelago client and adds the slot")]
        [CommandParameter("slot", "Slot to tie to in the Archipelago client", ApplicationCommandOptionType.String, true)]
        [CommandParameter("notify", "Whether or not to notify the user on discord", ApplicationCommandOptionType.Boolean, false)]
        [CommandParameter("user", "User to tie to this client. If left empty, it will be the current caller", ApplicationCommandOptionType.User, false)]
        public static async Task Slot(string slot, bool notify = false, SocketUser user = null, SocketSlashCommand command = null)
        {
            if (!command.GuildId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a discord server");
                return;
            }

            if (!command.ChannelId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a server channel.");
                return;
            }

            if (user == null)
            {
                user = command.User;
            }

            await command.RespondAndLog($"Assigning slot ({slot}) for user {user.GlobalName} in this channel. This may take a few seconds.");
            await ArchipelagoSessionManager.ConnectPlayer(command.GuildId.Value, command.ChannelId.Value, slot, notify, user.Id);
            await command.Channel.SendMessageAndLog($"Slot {slot} was assigned.");
        }

        [Command("detach", "Detached the discord user from the registered slots in this channel")]
        [CommandParameter("user", "User to tie to this client. If left empty, it will be the current caller", ApplicationCommandOptionType.User, false)]
        public static async Task Detach(SocketUser user = null, SocketSlashCommand command = null)
        {
            if (!command.GuildId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a discord server");
                return;
            }

            if (!command.ChannelId.HasValue)
            {
                await command.RespondAndLog("This command should be called in a server channel.");
                return;
            }

            if (user == null)
            {
                user = command.User;
            }

            bool isSessionStillOpen = await ArchipelagoSessionManager.DetachPlayer(command.GuildId.Value, command.ChannelId.Value, user.Id);
            await command.RespondAndLog($"Dettached slots for user {user.GlobalName} in this channel. {(!isSessionStillOpen ? "All users have been dettached. The session was closed." : "")}");
        }
    }
}
