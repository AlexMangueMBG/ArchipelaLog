using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.Enums;

namespace ArchipelaLog.Models
{
    public class ItemUpdate
    {
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string ItemName { get; set; }
        public string ItemLocation { get; set; }
        public ItemFlags ItemFlags { get; set; }
    }
}
