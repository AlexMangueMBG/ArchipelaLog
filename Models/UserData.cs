using System.ComponentModel;
using Discord;

namespace ArchipelaLog.Models
{
    public class UserData
    {
        public bool IsAttached { get; set; }
        public ulong DiscordId { get; set; }
        public string SlotName { get; set; }
        public bool Notify { get; set; }
        public List<ItemUpdate> ItemData { get; set; }

        public string GetDisplayName(bool mention = false)
        {
            if (IsAttached)
            {
                Task<ValueTask<IUser>> task = Task.Run(() => DiscordBot.Client.GetUserAsync(DiscordId));
                task.Wait();
                return (Notify && mention) ? task.Result.Result.Mention : task.Result.Result.GlobalName;
            }
            else
            {
                return SlotName;
            }
        }
    }
}
