using BepInEx;
using BepInEx.IL2CPP;
using CrabDevKit.Intermediary;
using CrabDevKit.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CustomGameModes
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("lammas123.CrabDevKit")]
    public class CustomGameModes : BasePlugin
    {
        internal static CustomGameModes Instance;

        internal Dictionary<ulong, Dictionary<string, int>> clientCustomGameModes = [];

        internal bool shouldSendHostCustomGameModes = false;

        internal PreloadingState preloadingState = PreloadingState.None;
        internal int preloadingMapId = -1;
        internal int postloadingMapId = -1;
        internal HashSet<ulong> clientIdsPreloading = [];

        internal const string CLIENT_CUSTOM_GAME_MODES = $"{MyPluginInfo.PLUGIN_GUID}:CustomGameModes";
        
        public override void Load()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Instance = this;

            Api.RegisterCustomGameMode(new CustomGameModeBaseball());
            Api.RegisterCustomGameMode(new CustomGameModeStandoff());

            CrabNet.RegisterMessageHandler(CLIENT_CUSTOM_GAME_MODES, ClientCustomGameModes);

            Harmony.CreateAndPatchAll(typeof(Patches));
            Log.LogInfo($"Initialized [{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION}]");
        }

        internal void CheckPreloadingFinished()
        {
            if (clientIdsPreloading.Count == 0)
            {
                // No clients are preloading anymore, we're good to go
                FinishedPreloading();
                return;
            }

            foreach (ulong clientId in clientIdsPreloading)
                if (LobbyManager.Instance.GetClient(clientId).field_Public_Boolean_0)
                    return; // A client that is still preloading is participating and would be playing next round, continue preloading

            // All clients currently still preloading are not participating and will be spectators, no need to wait on them
            FinishedPreloading();
        }
        internal void FinishedPreloading()
        {
            preloadingState = PreloadingState.Finished;

            // Eliminate anyone that didn't preload before preloading finished
            foreach (ulong clientId in Instance.clientIdsPreloading)
                LobbyManager.Instance.GetClient(clientId).field_Public_Boolean_0 = false;
            clientIdsPreloading.Clear();

            foreach (ulong clientId in LobbyManager.steamIdToUID.Keys)
                ServerSend.LoadMap(postloadingMapId, LobbyManager.Instance.gameMode.id, clientId);
            preloadingMapId = -1;
            postloadingMapId = -1;
        }

        internal void ClientCustomGameModes(ulong clientId, Packet packet)
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return;

            int gameModes = packet.ReadInt();
            Dictionary<string, int> customGameModes = [];

            for (int i = 0; i < gameModes; i++)
            {
                string gameModeName = packet.ReadString();
                string gameModeVersion = packet.ReadString();
                int gameModeId = packet.ReadInt();

                customGameModes[gameModeName] = gameModeId;

                if (Api.customGameModes.ContainsKey(gameModeName))
                {
                    Api.customGameModes[gameModeName].ClientsWithGameMode[clientId] = gameModeId;
                    Api.customGameModes[gameModeName].ClientGameModeVersions[clientId] = gameModeVersion;
                }
            }

            clientCustomGameModes[clientId] = customGameModes;
            ServerSend.LoadMap(LobbyManager.Instance.map.id, LobbyManager.Instance.gameMode.id, clientId);
        }
    }

    internal enum PreloadingState
    {
        None = -1,
        InProgress,
        Finished
    }
}