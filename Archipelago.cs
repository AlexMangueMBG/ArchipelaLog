using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;

namespace ArchipelaLog
{
    public class Archipelago
    {
        private ArchipelagoSession _session;
        private bool IsLogAttached = false;

        [XmlIgnore]
        public Guid Guid;

        public static async Task<IEnumerable<ScoutedItemInfo>> Received(string hostname, int port, string slotName)
        {
            ArchipelagoSession temporalSession;

            try
            {
                temporalSession = ArchipelagoSessionFactory.CreateSession(hostname, port);
            }
            catch(Exception ex)
            {
                throw new Exception($"Failed to create archipelago session: {ex.Message}");
            }

            try
            {
                LoginResult loginResult = temporalSession.TryConnectAndLogin("", slotName, ItemsHandlingFlags.AllItems, tags: ["TextOnly", "AP"]);

                if (!loginResult.Successful)
                {
                    LoginFailure failure = (LoginFailure)loginResult;
                    string errorMessage = "";

                    foreach (string error in failure.Errors)
                    {
                        errorMessage += $"\n    {error}";
                    }
                    foreach (ConnectionRefusedError error in failure.ErrorCodes)
                    {
                        errorMessage += $"\n    {error}";
                    }

                    throw new Exception(errorMessage);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect as {slotName}: {ex.Message}");
            }

            Dictionary<long, ScoutedItemInfo> checkedLocations = await temporalSession.Locations.ScoutLocationsAsync(HintCreationPolicy.None, temporalSession.Locations.AllLocationsChecked.ToArray());

            await temporalSession.Socket.DisconnectAsync();

            return checkedLocations.Select(location => location.Value);
        }

        public async Task Login(string hostname, int port, string slotName)
        {
            if(_session == null)
            {
                try
                {
                    this._session = ArchipelagoSessionFactory.CreateSession(hostname, port);
                    this._session.MessageLog.OnMessageReceived += OnMessageReceived;
                    this._session.Socket.SocketClosed += OnSocketClosed;
                    this.IsLogAttached = true;
                }
                catch (Exception ex)
                {
                    this._session = null;
                    throw new Exception($"Failed to create archipelago session: {ex.Message}");
                }

                try
                {
                    LoginResult loginResult = this._session.TryConnectAndLogin("", slotName, ItemsHandlingFlags.AllItems, tags: ["TextOnly", "AP"]);

                    if (!loginResult.Successful)
                    {
                        LoginFailure failure = (LoginFailure)loginResult;
                        string errorMessage = "";

                        foreach (string error in failure.Errors)
                        {
                            errorMessage += $"\n    {error}";
                        }
                        foreach (ConnectionRefusedError error in failure.ErrorCodes)
                        {
                            errorMessage += $"\n    {error}";
                        }

                        this._session = null;
                        throw new Exception(errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    this._session = null;
                    throw new Exception($"Failed to connect as {slotName}: {ex.Message}");
                }

                this.Guid = Guid.NewGuid();
            }
        }

        public void Say(string command)
        {
            if(this._session != null)
            {
                try
                {
                    this._session.Say(command);
                }
                catch(Exception ex)
                {
                    throw new Exception($"Failed to run {command}: {ex.Message}");
                }
            }
        }

        public void DettachEventHandler()
        {
            if(IsLogAttached)
            {
                _session.MessageLog.OnMessageReceived -= OnMessageReceived;
                IsLogAttached = false;
            }
        }

        private void OnMessageReceived(LogMessage message)
        {
            bool notified = false;

            switch (message)
            {
                case ItemSendLogMessage itemSendLogMessage:
                    if (message.ToString().Contains("[Hint]"))
                        break;

                    Task.Run(() => ArchipelagoSessionManager.NotifyPlayers(itemSendLogMessage, this));
                    notified = true;
                    break;
            }
            
            if (!notified)
                Console.WriteLine(message);
        }

        private void OnSocketClosed(string reason)
        {
            Console.WriteLine($"SOCKET CLOSED BECAUSE: {reason}");
        }

        public async Task Disconnect()
        {
            if(this._session != null)
            {
                await this._session.Socket.DisconnectAsync();
            }
        }
    }
}
