using SteamworksNative;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CustomGameModes
{
    internal static class Utility
    {
        internal static int MaxChatMessageLength
            => Chatbox.Instance != null ? Chatbox.Instance.field_Private_Int32_0 : 80;

        internal static string FormatMessage(string str)
            => Regex.Replace(
                str,
                "(.)(?<=\\1{5})", // Remove repeating characters (5 or more will truncate to 4, allowing it to appear in chat)
                string.Empty,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled
             );

        internal static string[] FormatGameModeDescription(string description)
        {
            List<string> lines = [.. description.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = lines[i].Replace('•', '*');
                while (lines[i].Length > MaxChatMessageLength)
                {
                    int split = lines[i].LastIndexOf(' ', MaxChatMessageLength - 1, 20);
                    if (split == -1)
                        split = MaxChatMessageLength;
                    lines.Insert(i + 1, lines[i][split..]);
                    lines[i] = lines[i][..split];
                }
            }
            return [.. lines];
        }
        internal static void SendCustomGameModeNotice(GameModeData gameModeData, ulong clientId)
        {
            SendMessage(clientId, $"The current mode is actually {gameModeData.modeName}!", MessageType.Styled, "NOTICE");
            foreach (string line in FormatGameModeDescription(gameModeData.modeDescription))
                SendMessage(clientId, line, MessageType.Styled);
        }

        internal enum MessageType
        {
            Normal,
            Server,
            Styled
        }
        internal static void SendMessage(ulong recipientClientId, string message, MessageType messageType = MessageType.Server, string displayName = null)
            => SendMessage(message, messageType, displayName, [recipientClientId]);
        internal static void SendMessage(string message, MessageType messageType = MessageType.Server, string displayName = null, IEnumerable<ulong> recipientClientIds = null)
        {
            ulong senderClientId = 0UL;
            message ??= string.Empty;
            message = FormatMessage(message);
            if (messageType == MessageType.Server)
            {
                displayName = string.Empty;
                senderClientId = 1UL;
            }
            else
                displayName ??= string.Empty;

            List<byte> bytes = [];
            bytes.AddRange(BitConverter.GetBytes((int)ServerPackets.sendMessage));
            bytes.AddRange(BitConverter.GetBytes(senderClientId));

            bytes.AddRange(BitConverter.GetBytes(displayName.Length));
            bytes.AddRange(Encoding.ASCII.GetBytes(displayName));

            bytes.AddRange(BitConverter.GetBytes(message.Length));
            bytes.AddRange(Encoding.ASCII.GetBytes(message));

            bytes.InsertRange(0, BitConverter.GetBytes(bytes.Count));

            Packet packet = new();
            packet.field_Private_List_1_Byte_0 = new();
            foreach (byte b in bytes)
                packet.field_Private_List_1_Byte_0.Add(b);

            foreach (ulong clientId in recipientClientIds ?? [.. LobbyManager.steamIdToUID.Keys])
            {
                if (messageType == MessageType.Styled)
                {
                    byte[] clientIdBytes = BitConverter.GetBytes(clientId);
                    for (int i = 0; i < clientIdBytes.Length; i++)
                        packet.field_Private_List_1_Byte_0[i + 8] = clientIdBytes[i];
                }
                SteamPacketManager.SendPacket(new CSteamID(clientId), packet, 8, SteamPacketManager_NetworkChannel.ToClient);
            }
        }
    }
}