using BepInEx.IL2CPP.Utils;
using CrabDevKit.Intermediary;
using HarmonyLib;
using SteamworksNative;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CustomGameModes
{
    public sealed class CustomGameModeStandoff : CustomGameMode
    {
        internal static CustomGameModeStandoff Instance;
        internal static Deobf_GameModeStandoff Standoff;
        internal static bool processStandoffFiredShot = true;
        internal static bool shouldDrop = true;
        internal static bool replaceCurrentAmmo = false;
        internal Harmony patches;

        public CustomGameModeStandoff() : base
        (
            name: "Standoff",
            description: "• You can only shoot when signaled\n\n• Hit someone to give them a \"mark\"\n\n• Bullets left in your gun count as \"marks\"\n\n• Most marked players are eliminated",
            gameModeType: GameModeData_GameModeType.Standoff,
            vanillaGameModeType: GameModeData_GameModeType.HatKing,
            
            shortModeTime: 45,
            mediumModeTime: 55,
            longModeTime: 60,

            compatibleMapNames: [
                "Blueline",
                "Return to Monke",
                "Funky Field",
                "Sandstorm",
                "Small Beach",
                "Tiny Town",
                "Playground2",
                "Twisted Towers",
                "Lanky Lava",
                "Bitter Beach",
                "Karlson",
                "Mini Monke",
                "Sunny Saloon",
                "Tiny Town 2",
                "Small Saloon",
                "Toxic Train",
                "Cocky Containers",
                "Small Containers",
                "Icy Crack"
            ]
        )
            => Instance = this;

        public override void PreInit()
            => patches = Harmony.CreateAndPatchAll(GetType());
        public override void PostEnd()
            => patches?.UnpatchSelf();

        internal bool CanShoot(ulong clientId)
            => ClientsWithGameMode.ContainsKey(clientId) || Standoff.canShoot;
        internal IEnumerator SyncVanillaPenalties()
        {
            while (true)
            {
                if (LobbyManager.steamIdToUID.Count - Instance.ClientsWithGameMode.Count != 0)
                {
                    List<byte> bytes = [];
                    bytes.AddRange(BitConverter.GetBytes((int)ServerPackets.hatScores));
                    bytes.AddRange(BitConverter.GetBytes(Standoff.standoffPlayers.Count));

                    foreach (ulong clientId in Standoff.standoffPlayers.Keys)
                    {
                        bytes.AddRange(BitConverter.GetBytes(clientId));
                        bytes.AddRange(BitConverter.GetBytes(-Standoff.standoffPlayers[clientId].Method_Public_Int32_0()));
                    }
                    bytes.InsertRange(0, BitConverter.GetBytes(bytes.Count));

                    Packet packet = new();
                    packet.field_Private_List_1_Byte_0 = new();
                    foreach (byte b in bytes)
                        packet.field_Private_List_1_Byte_0.Add(b);

                    foreach (ulong clientId in LobbyManager.steamIdToUID.Keys)
                        if (!Instance.ClientsWithGameMode.ContainsKey(clientId))
                            SteamPacketManager.SendPacket(new CSteamID(clientId), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
                }
                yield return new WaitForSeconds(5);
            }
        }


        // Begin syncing vanilla player penalties every 5 seconds
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.InitMode))]
        [HarmonyPostfix]
        internal static void PostInitMode(Deobf_GameModeStandoff __instance)
        {
            Standoff = __instance;

            if (SteamManager.Instance.IsLobbyOwner())
                __instance.StartCoroutine(Instance.SyncVanillaPenalties());
        }

        // Overwrite FindPlayersToKill, making it a bit more fair how many players are killed
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.FindPlayersToKill))]
        [HarmonyPrefix]
        internal static bool PreFindPlayersToKill(ref int __result)
        {
            __result = Mathf.RoundToInt(GameManager.Instance.GetPlayersAlive() * 0.3f);
            return false;
        }

        // Add and remove standoffPlayers as players join and leave (avoids errors at the end of the game when trying to kill players that left the game before dying)
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.OnPlayerSpawnOrDespawn))]
        [HarmonyPostfix]
        internal static void PostOnPlayerSpawnOrDespawn(Deobf_GameModeStandoff __instance, ulong param_1)
        {
            if (GameManager.Instance.activePlayers.ContainsKey(param_1) && !__instance.standoffPlayers.ContainsKey(param_1) && !GameManager.Instance.activePlayers[param_1].dead)
                __instance.standoffPlayers.Add(param_1, new(GameManager.Instance.activePlayers[param_1]));
            else if (!GameManager.Instance.activePlayers.ContainsKey(param_1) && __instance.standoffPlayers.ContainsKey(param_1))
                __instance.standoffPlayers.Remove(param_1);
        }

        // Override OnFreezeOver and inform players that they should wait to shoot
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.OnFreezeOver))]
        [HarmonyPrefix]
        internal static bool PreOnFreezeOver(Deobf_GameModeStandoff __instance)
        {
            __instance.field_Private_Int32_0 = __instance.FindPlayersToKill();
            foreach (ulong clientId in GameManager.Instance.activePlayers.Keys)
                if (GameManager.Instance.activePlayers.ContainsKey(clientId) && !GameManager.Instance.activePlayers[clientId].dead && !__instance.standoffPlayers.ContainsKey(clientId))
                    __instance.standoffPlayers.Add(clientId, new(GameManager.Instance.activePlayers[clientId]));

            if (!SteamManager.Instance.IsLobbyOwner())
                return false;

            shouldDrop = false;
            GameServer.ForceGiveAllWeapon(2); // Revolver
            shouldDrop = true;
            __instance.Invoke("SendToggle", __instance.activeTime);

            Utility.SendMessage("Hold your fire until the next notice! You have 6 shots.", Utility.MessageType.Styled, "Standoff");
            return false;
        }

        // Inform players of toggle
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.ToggleShoot))]
        [HarmonyPostfix]
        internal static void PostToggleShoot(Deobf_GameModeStandoff __instance)
        {
            if (SteamManager.Instance.IsLobbyOwner())
                Utility.SendMessage($"You can {(__instance.canShoot ? "NOW" : "NO LONGER")} shoot!", Utility.MessageType.Styled, "Standoff");
        }

        // Overwrite ShotPlayer, prevent shots when player can't shoot, and inform player that they hit another player
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.ShotPlayer))]
        [HarmonyPrefix]
        internal static bool PreShotPlayer(Deobf_GameModeStandoff __instance, ulong param_1, ulong param_2)
        {
            if (!__instance.CanFire(param_1) || !Instance.CanShoot(param_1))
                return false;

            __instance.standoffPlayers[param_2].field_Public_Int32_1++;
            __instance.standoffPlayers[param_1].field_Public_Int32_1--;
            ServerSend.StandoffUpdate(-1, __instance.standoffPlayers[param_2].field_Public_Int32_1, param_2, param_1);

            Utility.SendMessage(param_1, $"Hit {SteamFriends.GetFriendPersonaName(new(param_2))}!", Utility.MessageType.Styled, "Standoff");
            return false;
        }

        // Override ShotFired, prevent shots from occuring when they couldn't haven, and inform the player of their remaining shots
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.ShotFired))]
        [HarmonyPrefix]
        internal static bool PreShotFired(Deobf_GameModeStandoff __instance, ulong param_1)
        {
            if (!processStandoffFiredShot)
                return false;
            if (!Instance.CanShoot(param_1))
            {
                Utility.SendMessage(param_1, "Shots do not count at this time.", Utility.MessageType.Styled, "Standoff");
                shouldDrop = false;
                GameServer.ForceGiveWeapon(param_1, 2, SharedObjectManager.Instance.GetNextId());
                shouldDrop = true;
                return false;
            }

            // Process shot
            __instance.standoffPlayers[param_1].field_Public_Int32_0--;
            __instance.standoffPlayers[param_1].field_Public_Int32_1++;
            ServerSend.StandoffUpdate(__instance.standoffPlayers[param_1].field_Public_Int32_0, __instance.standoffPlayers[param_1].field_Public_Int32_1, param_1, 0UL);
            Utility.SendMessage(param_1, $"Remaining shots: {__instance.standoffPlayers[param_1].field_Public_Int32_0}/6", Utility.MessageType.Styled, "Standoff");
            if (!__instance.CanFire(param_1))
            {
                GameServer.ForceRemoveItemItemId(param_1, 2);
                Utility.SendMessage(param_1, "You've ran out of bullets, avoid getting shot by others!", Utility.MessageType.Styled, "Standoff");
            }

            // Nobody has any more bullets
            if (__instance.Method_Private_Boolean_0())
            {
                __instance.CancelInvoke("SendToggle");
                ServerSend.StandoffToggle(false);

                // Shorten time
                if (__instance.freezeTimer.field_Private_Single_0 > 5f)
                {
                    __instance.freezeTimer.field_Private_Single_0 = 5f;
                    ServerSend.SendGameModeTimer(__instance.freezeTimer.field_Private_Single_0, (int)__instance.modeState);
                }
            }

            return false;
        }

        // Updates ammo ui
        [HarmonyPatch(typeof(Deobf_GameModeStandoff), nameof(Deobf_GameModeStandoff.StandoffUpdate))]
        [HarmonyPostfix]
        internal static void PostStandoffUpdate(int param_1, ulong param_3)
        {
            if (param_3 == SteamManager.Instance.get_PlayerSteamId().m_SteamID && param_1 != -1)
                PlayerInventory.Instance.UpdateAmmoUI();
        }


        // Prevents being able to use other items and have them count as shots
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.UseItem))]
        [HarmonyPrefix]
        internal static void PreServerHandleUseItem(Packet param_1)
            => processStandoffFiredShot = BitConverter.ToInt32(param_1.field_Private_ArrayOf_Byte_0, 8) == 2;

        // Prevents being able to drop items
        [HarmonyPatch(typeof(ServerHandle), nameof(ServerHandle.TryDropItem))]
        [HarmonyPrefix]
        internal static bool PreServerHandleTryDropItem()
            => false;

        // Prevents seeing other players firing when they cannot
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.UseItem))]
        [HarmonyPrefix]
        internal static bool PreServerSendUseItem(ulong param_0, int param_1)
            => param_1 != 2 || (Instance.CanShoot(param_0) && Standoff.CanFire(param_0));

        // Prevents other's hidden shots from dealing knockback/damage
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.PlayerDamage))]
        [HarmonyPrefix]
        internal static bool PreServerSendPlayerDamage(ulong param_0, int param_4)
            => param_4 != 2 || (Instance.CanShoot(param_0) && Standoff.CanFire(param_0));

        // Prevents dropping the player's currently held Revolver when giving a new one
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.DropItem))]
        [HarmonyPrefix]
        internal static bool PreServerSendDropItem()
            => shouldDrop;

        // Override StandoffToggle, prevents StandoffToggle errors on vanilla clients, and give everyone with bullets a new revolver when they can no longer shoot
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.StandoffToggle))]
        [HarmonyPrefix]
        internal static bool PreServerSendStandoffToggle(bool param_0)
        {
            List<byte> bytes = [];
            bytes.AddRange(BitConverter.GetBytes((int)ServerPackets.standoffToggle));
            bytes.AddRange(BitConverter.GetBytes(param_0));
            bytes.InsertRange(0, BitConverter.GetBytes(bytes.Count));

            Packet packet = new();
            packet.field_Private_List_1_Byte_0 = new();
            foreach (byte b in bytes)
                packet.field_Private_List_1_Byte_0.Add(b);

            foreach (ulong clientId in Instance.ClientsWithGameMode.Keys)
                SteamPacketManager.SendPacket(new CSteamID(clientId), packet, 8, SteamPacketManager_NetworkChannel.ToClient);

            if (!param_0)
            {
                shouldDrop = false;
                foreach (ulong clientId in LobbyManager.steamIdToUID.Keys)
                    if (Standoff.standoffPlayers.ContainsKey(clientId) && Standoff.CanFire(clientId))
                        GameServer.ForceGiveWeapon(clientId, 2, SharedObjectManager.Instance.GetNextId());
                shouldDrop = true;
            }
            return false;
        }

        // Override StandoffUpdate, prevents StandoffUpdate errors on vanilla clients
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.StandoffUpdate))]
        [HarmonyPrefix]
        internal static bool PreServerSendStandoffUpdate(int param_0, int param_1, ulong param_2, ulong param_3)
        {
            List<byte> bytes = [];
            bytes.AddRange(BitConverter.GetBytes((int)ServerPackets.standoffUpdate));
            bytes.AddRange(BitConverter.GetBytes(param_0));
            bytes.AddRange(BitConverter.GetBytes(param_1));
            bytes.AddRange(BitConverter.GetBytes(param_2));
            bytes.AddRange(BitConverter.GetBytes(param_3));
            bytes.InsertRange(0, BitConverter.GetBytes(bytes.Count));

            Packet packet = new();
            packet.field_Private_List_1_Byte_0 = new();
            foreach (byte b in bytes)
                packet.field_Private_List_1_Byte_0.Add(b);

            foreach (ulong clientId in Instance.ClientsWithGameMode.Keys)
                SteamPacketManager.SendPacket(new CSteamID(clientId), packet, 8, SteamPacketManager_NetworkChannel.ToClient);


            bytes = [];
            bytes.AddRange(BitConverter.GetBytes((int)ServerPackets.hatScores));
            bytes.AddRange(BitConverter.GetBytes(1));

            bytes.AddRange(BitConverter.GetBytes(param_2));
            bytes.AddRange(BitConverter.GetBytes(-Standoff.standoffPlayers[param_2].Method_Public_Int32_0()));
            bytes.InsertRange(0, BitConverter.GetBytes(bytes.Count));

            packet = new();
            packet.field_Private_List_1_Byte_0 = new();
            foreach (byte b in bytes)
                packet.field_Private_List_1_Byte_0.Add(b);

            foreach (ulong clientId in LobbyManager.steamIdToUID.Keys)
                if (!Instance.ClientsWithGameMode.ContainsKey(clientId))
                    SteamPacketManager.SendPacket(new CSteamID(clientId), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
            return false;
        }

        // Sets the amount of ammo to show in the bottom right of the screen to the amount of bullets you have left, rather than how many bullets are in your gun
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.UpdateAmmoUI))]
        [HarmonyPrefix]
        internal static void PrePlayerInventoryUpdateAmmoUI()
            => replaceCurrentAmmo = true;
        [HarmonyPatch(typeof(CircleRatioUI), nameof(CircleRatioUI.SetRatio))]
        [HarmonyPrefix]
        internal static void PreCircleRatioUISetRatio(ref int param_1)
        {
            if (replaceCurrentAmmo)
                param_1 = Standoff.standoffPlayers[SteamManager.Instance.get_PlayerSteamId().m_SteamID]?.field_Public_Int32_0 ?? 0;
        }
        [HarmonyPatch(typeof(PlayerInventory), nameof(PlayerInventory.UpdateAmmoUI))]
        [HarmonyPostfix]
        internal static void PostPlayerInventoryUpdateAmmoUI()
            => replaceCurrentAmmo = false;
    }
}