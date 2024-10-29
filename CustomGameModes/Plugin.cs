using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CustomGameModes
{
    [BepInPlugin($"lammas123.{MyPluginInfo.PLUGIN_NAME}", MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class CustomGameModes : BasePlugin
    {
        internal static CustomGameModes Instance;

        internal Dictionary<ulong, Dictionary<string, int>> clientCustomGameModes = [];

        internal bool shouldSendHostCustomGameModes = false;

        internal PreloadingState preloadingState = PreloadingState.None;
        internal int preloadingMapId = -1;
        internal HashSet<ulong> clientIdsPreloading = [];

        public override void Load()
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            Instance = this;

            Api.RegisterCustomGameMode(new CustomGameModeBaseball());
            Api.RegisterCustomGameMode(new CustomGameModeStandoff());

            CrabNetLib.RegisterServerMessageHandler(nameof(ClientCustomGameModes), ClientCustomGameModes);

            Harmony.CreateAndPatchAll(typeof(Patches));
            Log.LogInfo($"Loaded [{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION}]");
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
                ServerSend.LoadMap(preloadingMapId, LobbyManager.Instance.gameMode.id, clientId);
            preloadingMapId = -1;
        }

        internal void ClientCustomGameModes(ulong clientId, byte[] bytes)
        {
            int index = 0;
            int count = BitConverter.ToInt32(bytes, index);
            index += 4;

            Dictionary<string, int> customGameModes = [];
            for (int i = 0; i < count; i++)
            {
                int customGameModeNameLength = BitConverter.ToInt32(bytes, index);
                index += 4;
                string customGameModeName = Encoding.ASCII.GetString(bytes, index, customGameModeNameLength);
                index += customGameModeNameLength;
                customGameModes[customGameModeName] = BitConverter.ToInt32(bytes, index);
                index += 4;

                if (Api.customGameModes.ContainsKey(customGameModeName))
                    Api.customGameModes[customGameModeName].ClientsWithGameMode[clientId] = customGameModes[customGameModeName];
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