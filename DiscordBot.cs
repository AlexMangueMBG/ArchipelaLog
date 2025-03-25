using Discord;
using Discord.Net;
using Discord.WebSocket;
using Newtonsoft.Json;
using System.Reflection;
using System.Threading.Channels;
using static ArchipelaLog.CommandAttribute;

namespace ArchipelaLog
{
    public static class DiscordBot
    {
        public static DiscordSocketClient Client;
        private static Dictionary<string, MethodInfo> _registeredCommands = new Dictionary<string, MethodInfo>();

        public static async Task Setup()
        {
            Client = new DiscordSocketClient();
            Client.Log += Log;
            Client.Ready += Client_Ready;
            Client.SlashCommandExecuted += SlashCommandHandler;

            string token = string.Empty;

            if (Environment.GetEnvironmentVariable("BotToken") != null)
                token = Environment.GetEnvironmentVariable("BotToken");

            if(File.Exists(@$"{Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)}/BotToken.txt"))
                token = File.ReadAllText(@$"{Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)}/BotToken.txt");

            if(string.IsNullOrEmpty(token))
                throw new Exception("Failed to find discord bot token. You can either add an environment variable called \"BotToken\" with your token on it. Or, if you prefer it, you can create text file called \"BotToken.txt\" on the exe folder and set your token there.");

            await Client.LoginAsync(TokenType.Bot, token);
            await Client.StartAsync();
        }

        private static Task Log(Discord.LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private static async Task Client_Ready()
        {
            await Build_Commands();
        }

        private static async Task Build_Commands()
        {
            foreach (var method in typeof(Commands).GetMethods().Where(m => m.GetCustomAttributes(typeof(CommandAttribute), false).Length > 0))
            {
                CommandAttribute commandData = method.GetCustomAttribute(typeof(CommandAttribute), false) as CommandAttribute;

                var globalCommand = new SlashCommandBuilder()
                    .WithName(commandData.Name)
                    .WithDescription(commandData.Description);

                foreach (CommandParameterAttribute paramAttribute in method.GetCustomAttributes(typeof(CommandParameterAttribute), false))
                    globalCommand.AddOption(paramAttribute.Name, paramAttribute.OptionType, paramAttribute.Description, paramAttribute.Required);

                try
                {
                    await Client.CreateGlobalApplicationCommandAsync(globalCommand.Build());
                    _registeredCommands.Add(commandData.Name, method);

                    Console.WriteLine($"Registered command {commandData.Name}.");
                }
                catch (HttpException exception)
                {
                    var json = JsonConvert.SerializeObject(exception.Errors, Formatting.Indented);

                    Console.WriteLine(json);
                }
            }
        }

        private static async Task SlashCommandHandler(SocketSlashCommand command)
        {
            // Validate that the command has been registered
            if (!_registeredCommands.TryGetValue(command.Data.Name, out MethodInfo method))
            {
                await command.RespondAndLog($"Command {command.Data.Name} is not registered for this bot.");
                return;
            }

            CommandAttribute commandData = method.GetCustomAttribute(typeof(CommandAttribute), false) as CommandAttribute;

            SocketGuildUser user = Client.GetGuild(command.GuildId.Value).GetUser(command.User.Id);

            // Get the user roles as Discord Role enum
            IEnumerable<DiscordRoles> userRoles = user
                .Roles
                .Where(role => Enum.TryParse(role.Name, out DiscordRoles roleFlag))
                .Select(role => Enum.Parse<DiscordRoles>(role.Name));

            bool isAdmin = user.GuildPermissions.Administrator;

            // If the role is not included, do not run the command
            if (!isAdmin && commandData.Role != DiscordRoles.None && !userRoles.Any(uRole => commandData.Role.HasFlag(uRole)))
            {
                await command.RespondAndLog($"You do not have the permissions to run this command.");
                return;
            }

            List<object> commandParams = method.GetParameters().Select(p => p.DefaultValue).ToList();

            foreach (var param in command.Data.Options)
                commandParams[method.GetParameters().ToList().FindIndex(a => a.Name.ToLower() == param.Name.ToLower())] = param.Value;

            commandParams[^1] = command;

            try
            {
                await (Task)method.Invoke(null, commandParams.ToArray());
            }
            catch (Exception ex)
            {
                if(command.HasResponded)
                    await command.Channel.SendMessageAndLog(ex.Message, true);
                else
                    await command.RespondAndLog(ex.Message, true);
            }
        }

        public static async Task RespondAndLog(this SocketSlashCommand command, string message, bool error = false)
        {
            if (error)
            {
                Embed embed = new EmbedBuilder
                {
                    Color = Color.Red,
                    Title = "Something failed while completing action",
                    Description = message
                }.Build();

                await command.RespondAsync(embed: embed);
            }
            else
            {
                await command.RespondAsync(message);
            }
            Log(message);
        }

        public static async Task SendMessageAndLog(this ISocketMessageChannel channel, string message, bool error = false, MessageComponent components = null)
        {
            if(error)
            {
                Embed embed = new EmbedBuilder
                {
                    Color = Color.Red,
                    Title = "Something failed while completing action",
                    Description = message
                }.Build();

                await channel.SendMessageAsync(embed: embed, components: components);
            }
            else
            {
                await channel.SendMessageAsync(message, components: components);
            }

            Log(message);
        }

        public static void Log(string message)
        {
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm:ss")} {message}");
        }
    }
}
