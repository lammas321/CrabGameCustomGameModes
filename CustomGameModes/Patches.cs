using CrabDevKit.Intermediary;
using CrabDevKit.Utilities;
using HarmonyLib;
using SteamworksNative;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CustomGameModes.CustomGameModes;

namespace CustomGameModes
{
    internal static class Patches
    {
        //   Add the custom game modes to GameModeManager
        [HarmonyPatch(typeof(GameModeManager), nameof(GameModeManager.Awake))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        internal static void PostGameModeManagerAwake()
            => Api.AddCustomGameModes();


        //   Makes game modes scale dynamically on the lobby creation screen
        [HarmonyPatch(typeof(Deobf_MenuUiCreateLobbyGameModesAndMaps), nameof(Deobf_MenuUiCreateLobbyGameModesAndMaps.Start))]
        [HarmonyPostfix]
        internal static void PostDeobf_MenuUiCreateLobbyGameModesAndMapsStart(Deobf_MenuUiCreateLobbyGameModesAndMaps __instance)
        {
            GridLayoutGroup gameModeGroup = __instance.modeContainer.GetComponent<GridLayoutGroup>();

            int gameModeColumns = Mathf.FloorToInt(gameModeGroup.preferredWidth / gameModeGroup.cellSize.x); // Should be 5
            int gameModeRows = Mathf.CeilToInt((float)(GameModeManager.Instance.allGameModes.Length - Api.SkippedGameModesCount) / gameModeColumns);

            __instance.modeContainer.parent.GetComponent<LayoutElement>().minHeight = (gameModeRows * (gameModeGroup.cellSize.y + gameModeGroup.spacing.y)) + gameModeGroup.spacing.y;
        }

        //   Override MenuUiServerListingGameModesAndMapsInfo.SetModes, to only iterate over the vanilla game modes, then add the custom game modes, and scale the ui dynamically
        internal static float defaultGameModesAndMapsTextSpacing = 0;
        internal static float defaultMapsTextAndContainerSpacing = 0;
        [HarmonyPatch(typeof(Deobf_MenuUiServerListingGameModesAndMapsInfo), nameof(Deobf_MenuUiServerListingGameModesAndMapsInfo.SetModes))]
        [HarmonyPrefix]
        internal static bool PreDeobf_MenuUiServerListingGameModesAndMapsInfoSetModes(Deobf_MenuUiServerListingGameModesAndMapsInfo __instance, string param_1)
        {
            int index = 0;
            for (int i = 0; i < Api.VanillaGameModesCount; i++)
            {
                GameModeData gameModeData = GameModeManager.Instance.allGameModes[i];
                if (!gameModeData.skipAsString)
                    UnityEngine.Object.Instantiate(__instance.prefabText, __instance.modeContainer)
                        .GetComponent<TextMeshProUGUI>().text = $"<color={(param_1[index++] == '1' ? __instance.blueCol : __instance.redCol)}>{gameModeData.modeName}";
            }

            Dictionary<string, bool> customGameModes = [];
            string[] customGameModesData = param_1.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (customGameModesData.Length != 1)
            {
                customGameModesData = customGameModesData[1..];
                foreach (string customGameModeData in customGameModesData)
                {
                    string customGameModeName = customGameModeData[1..];
                    bool playable = customGameModeData[0] == '1';

                    UnityEngine.Object.Instantiate(__instance.prefabText, __instance.modeContainer)
                        .GetComponent<TextMeshProUGUI>().text = $"<color={(playable ? __instance.blueCol : __instance.redCol)}>{customGameModeName}";
                    customGameModes.Add(customGameModeName, playable);
                }
            }

            foreach (string customGameModeName in Api.customGameModes.Keys)
                if (!customGameModes.ContainsKey(customGameModeName))
                {
                    UnityEngine.Object.Instantiate(__instance.prefabText, __instance.modeContainer)
                        .GetComponent<TextMeshProUGUI>().text = $"<color={__instance.redCol}>{customGameModeName}";
                    customGameModes.Add(customGameModeName, false);
                }

            GridLayoutGroup gameModeGroup = __instance.modeContainer.GetComponent<GridLayoutGroup>();
            float gameModesHeight = Mathf.CeilToInt((float)(Api.VanillaGameModesCount + customGameModes.Count - Api.SkippedGameModesCount) / 2) * (gameModeGroup.cellSize.y + gameModeGroup.spacing.y) - gameModeGroup.spacing.y;
            float vanillaGameModesHeight = Mathf.CeilToInt((float)(Api.VanillaGameModesCount - Api.SkippedGameModesCount) / 2) * (gameModeGroup.cellSize.y + gameModeGroup.spacing.y) - gameModeGroup.spacing.y;

            if (defaultGameModesAndMapsTextSpacing == 0)
                defaultGameModesAndMapsTextSpacing = __instance.gameModeText.transform.localPosition.y - __instance.mapsModeText.transform.localPosition.y; // Should be 225
            if (defaultMapsTextAndContainerSpacing == 0)
                defaultMapsTextAndContainerSpacing = __instance.mapsModeText.transform.localPosition.y - __instance.mapContainer.localPosition.y; // Should be 147
            Vector3 position = __instance.mapsModeText.transform.localPosition;
            position.y = __instance.gameModeText.transform.localPosition.y - defaultGameModesAndMapsTextSpacing + vanillaGameModesHeight - gameModesHeight;
            __instance.mapsModeText.transform.localPosition = position;

            position = __instance.mapContainer.localPosition;
            position.y = __instance.mapsModeText.transform.localPosition.y - defaultMapsTextAndContainerSpacing;
            __instance.mapContainer.localPosition = position;
            return false;
        }
         
        
        //   Override GameModeManager.GetAvailableModesString, return the vanilla game modes as normal, as well as the custom game modes in a custom format
        [HarmonyPatch(typeof(GameModeManager), nameof(GameModeManager.GetAvailableModesString))]
        [HarmonyPrefix]
        internal static bool PreGameModeManagerGetAvailableModesString(GameModeManager __instance, ref string __result)
        {
            char[] modes = new char[Api.VanillaGameModesCount];
            foreach (GameModeData gameModeData in __instance.allGameModes)
            {
                if (gameModeData.id >= Api.VanillaGameModesCount)
                    break;
                if (gameModeData.skipAsString)
                    modes[gameModeData.id] = ' ';
                else if (__instance.allPlayableGameModes.Contains(gameModeData))
                    modes[gameModeData.id] = '1';
                else
                    modes[gameModeData.id] = '0';
            }

            __result = new string(modes).Replace(" ", "");
            foreach (CustomGameMode customGameMode in Api.customGameModes.Values)
                if (__instance.allPlayableGameModes.Contains(__instance.allGameModes[customGameMode.GameModeId]))
                    __result += $":1{customGameMode.Name}";
                else
                    __result += $":0{customGameMode.Name}";
            return false;
        }



        //   Set and initialize the current custom game mode, as well as finish the previous custom game mode
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Awake))]
        [HarmonyPostfix]
        internal static void PostGameManagerAwake()
        {
            if (Api.currentCustomGameMode != null)
            {
                // End the previous custom game mode
                Api.currentCustomGameMode.PostEnd();
                Api.currentCustomGameMode = null;
            }

            if (LobbyManager.Instance.gameMode.id < Api.VanillaGameModesCount) // No custom game mode is being played
                return;

            // Init the current custom game mode
            Api.currentCustomGameMode = Api.customGameModes[LobbyManager.Instance.gameMode.modeName];
            Api.currentCustomGameMode.PreInit();
        }


        //   Handles loading players into the right game modes and preloading the right maps
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.LoadMap), [typeof(int), typeof(int)])]
        [HarmonyPrefix]
        internal static bool PreServerSendLoadMap(int param_0, int param_1)
        {
            Instance.preloadingState = PreloadingState.None;
            Instance.clientIdsPreloading.Clear();
            if (param_1 >= Api.VanillaGameModesCount)
            {
                Instance.preloadingMapId = Api.customGameModes[GameModeManager.Instance.allGameModes[param_1].modeName].PreloadMapId;
                if (Instance.preloadingMapId != -1)
                {
                    Instance.preloadingState = PreloadingState.InProgress;
                    Instance.postloadingMapId = param_0;
                }
            }

            foreach (ulong clientId in LobbyManager.steamIdToUID.Keys)
                ServerSend.LoadMap(param_0, param_1, clientId);

            return false;
        }
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.LoadMap), [typeof(int), typeof(int), typeof(ulong)])]
        [HarmonyPrefix]
        internal static void PreServerSendLoadMapClient(ref int param_0, ref int param_1, ulong param_2)
        {
            if (param_1 < Api.VanillaGameModesCount)
                return;

            CustomGameMode customGameMode = Api.customGameModes[GameModeManager.Instance.allGameModes[param_1].modeName];
            if (param_2 != SteamManager.Instance.get_lobbyOwnerSteamId().m_SteamID)
            {
                param_1 = customGameMode.ClientsWithGameMode.ContainsKey(param_2)
                    ? customGameMode.ClientsWithGameMode[param_2]
                    : customGameMode.VanillaGameModeId;
            }

            if (Instance.preloadingState != PreloadingState.InProgress || Instance.clientIdsPreloading.Contains(param_2))
                return;

            Instance.clientIdsPreloading.Add(param_2);
            param_0 = Instance.preloadingMapId;
        }
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.GameModeLoaded))]
        [HarmonyPostfix]
        internal static void PostServerHandleGameModeLoaded(ulong param_0)
        {
            if (Instance.preloadingState != PreloadingState.InProgress)
                return;

            Instance.clientIdsPreloading.Remove(param_0);
            Instance.CheckPreloadingFinished();
        }
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.SendModeState))]
        [HarmonyPrefix]
        internal static bool PreServerSendSendModeState(int param_0)
        {
            if (Instance.preloadingState != PreloadingState.InProgress || param_0 == 0)
                return true;

            // Not every player was able to preload before the freeze phase ended, finish preloading anyway
            Instance.FinishedPreloading();
            return false;
        }


        //   Sends the actual game mode notice, or a preloading notice to players when they spawn
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SpawnPlayer))]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.SpawnSpectator))]
        [HarmonyPostfix]
        internal static void PostGameManagerSpawn(ulong param_1)
        {
            if (!SteamManager.Instance.IsLobbyOwner() || LobbyManager.Instance.gameMode.id < Api.VanillaGameModesCount)
                return;

            if (Instance.preloadingState == PreloadingState.InProgress)
                ChatUtil.SendMessage(param_1, "This map is being preloaded before the next game mode, please be patient.", ChatUtil.MessageType.Styled, "Preloading");
            else
            {
                ChatUtil.SendMessage(param_1, $"The current mode is actually {LobbyManager.Instance.gameMode.modeName}!", ChatUtil.MessageType.Styled, "NOTICE");
                foreach (string line in ChatUtil.FormatGameModeDescription(LobbyManager.Instance.gameMode.modeDescription))
                    ChatUtil.SendMessage(param_1, line, ChatUtil.MessageType.Styled);
            }
        }



        //   Sends the host your list of custom game modes upon requesting to join
        [HarmonyPatch(typeof(SteamManager), nameof(SteamManager.JoinLobby))]
        [HarmonyPrefix]
        internal static void PreSteamManagerJoinLobby()
            => Instance.shouldSendHostCustomGameModes = true;
        [HarmonyPatch(typeof(ClientHandle), nameof(ClientHandle.LoadMap))]
        [HarmonyPrefix]
        internal static void PreClientHandleLoadMap()
        {
            if (SteamManager.Instance.IsLobbyOwner() || !Instance.shouldSendHostCustomGameModes)
                return;

            Instance.shouldSendHostCustomGameModes = false;
            Packet packet = new();
            packet.Write(Api.customGameModes.Count);
            foreach (CustomGameMode customGameMode in Api.customGameModes.Values)
            {
                packet.Write(customGameMode.Name);
                packet.Write(customGameMode.Version);
                packet.Write(customGameMode.GameModeId);
            }
            
            CrabNet.SendMessage(CLIENT_CUSTOM_GAME_MODES, packet, SteamManager.Instance.get_lobbyOwnerSteamId().m_SteamID);
            packet.Dispose();
        }


        //   Handles the creation of your list of client custom game modes when you make a lobby
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.StartLobby))]
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.StartPracticeLobby))]
        [HarmonyPostfix]
        internal static void PostLobbyManagerStartLobby()
        {
            Instance.clientCustomGameModes.Clear();
            Instance.clientCustomGameModes[SteamManager.Instance.get_PlayerSteamId().m_SteamID] = [];
            foreach (CustomGameMode customGameMode in Api.customGameModes.Values)
            {
                Instance.clientCustomGameModes[SteamManager.Instance.get_PlayerSteamId().m_SteamID][customGameMode.Name] = customGameMode.GameModeId;
                customGameMode.ClientsWithGameMode.Clear();
                customGameMode.ClientGameModeVersions.Clear();
                customGameMode.ClientsWithGameMode[SteamManager.Instance.get_PlayerSteamId().m_SteamID] = customGameMode.GameModeId;
                customGameMode.ClientGameModeVersions[SteamManager.Instance.get_PlayerSteamId().m_SteamID] = customGameMode.Version;
            }
            
            Instance.preloadingState = PreloadingState.None;
            Instance.preloadingMapId = -1;
            Instance.clientIdsPreloading.Clear();
        }

        //   Handles the deletion of client custom game modes on player leave or lobby close
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.OnPlayerJoinLeaveUpdate))]
        [HarmonyPostfix]
        internal static void PostLobbyManagerOnPlayerJoinLeaveUpdate(CSteamID param_1, bool param_2)
        {
            if (!SteamManager.Instance.IsLobbyOwner() || param_2)
                return;

            Instance.clientCustomGameModes.Remove(param_1.m_SteamID);
            foreach (CustomGameMode customGameMode in Api.customGameModes.Values)
            {
                customGameMode.ClientsWithGameMode.Remove(param_1.m_SteamID);
                customGameMode.ClientGameModeVersions.Remove(param_1.m_SteamID);
            }
            
            if (Instance.preloadingState == PreloadingState.InProgress)
            {
                Instance.clientIdsPreloading.Remove(param_1.m_SteamID);
                Instance.CheckPreloadingFinished();
            }
        }
        [HarmonyPatch(typeof(LobbyManager), nameof(LobbyManager.CloseLobby))]
        [HarmonyPostfix]
        internal static void PostLobbyManagerCloseLobby()
        {
            Instance.clientCustomGameModes.Clear();
            foreach (CustomGameMode customGameMode in Api.customGameModes.Values)
            {
                customGameMode.ClientsWithGameMode.Clear();
                customGameMode.ClientGameModeVersions.Clear();
            }

            Instance.preloadingState = PreloadingState.None;
            Instance.preloadingMapId = -1;
            Instance.clientIdsPreloading.Clear();
        }
    }
}