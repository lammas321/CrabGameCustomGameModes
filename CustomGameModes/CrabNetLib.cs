using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CustomGameModes
{
    public static class CrabNetLib
    {
        internal static Dictionary<string, ClientMessageHandler> clientMessageHandlers = [];
        internal static Dictionary<string, ServerMessageHandler> serverMessageHandlers = [];
        
        public static void RegisterClientMessageHandler(string message, ClientMessageHandler messageHandler)
        {
            if (string.IsNullOrEmpty(message) || clientMessageHandlers.ContainsKey($"lammas123.{MyPluginInfo.PLUGIN_NAME}:{message}"))
                return;

            clientMessageHandlers.Add($"lammas123.{MyPluginInfo.PLUGIN_NAME}:{message}", messageHandler);
        }
        public static void RegisterServerMessageHandler(string message, ServerMessageHandler messageHandler)
        {
            if (string.IsNullOrEmpty(message) || serverMessageHandlers.ContainsKey($"lammas123.{MyPluginInfo.PLUGIN_NAME}:{message}"))
                return;
            
            serverMessageHandlers.Add($"lammas123.{MyPluginInfo.PLUGIN_NAME}:{message}", messageHandler);
        }

        public static void SendMessageToClient(ulong clientId, string message, IEnumerable<byte> bytes)
            => Send([clientId], message, bytes, SteamPacketDestination.ToClient);
        public static void SendMessageToServer(string message, IEnumerable<byte> bytes)
            => Send([Utility.HostClientId], message, bytes, SteamPacketDestination.ToServer);

        public static void Send(IEnumerable<ulong> clientIds, string message, IEnumerable<byte> bytes, SteamPacketDestination destination)
            => SendInternal(clientIds, $"lammas123.{MyPluginInfo.PLUGIN_NAME}:{message}", bytes, destination);
        private static void SendInternal(IEnumerable<ulong> clientIds, string message, IEnumerable<byte> bytes, SteamPacketDestination destination)
        {
            if (string.IsNullOrEmpty(message) || (destination == SteamPacketDestination.ToClient && !SteamManager.Instance.IsLobbyOwner()))
                return;

            Packet packet = new();
            packet.field_Private_List_1_Byte_0 = new();

            foreach (byte b in BitConverter.GetBytes(8 + message.Length + bytes.Count())) // Packet Length
                packet.field_Private_List_1_Byte_0.Add(b);
            foreach (byte b in BitConverter.GetBytes(                                     // Packet Type
                destination == SteamPacketDestination.ToClient ?
                (int)ServerSendType.lobbyMapUpdate :
                (int)ClientSendType.handShake
            ))
                packet.field_Private_List_1_Byte_0.Add(b);
            foreach (byte b in BitConverter.GetBytes(message.Length))                     // Message String Length
                packet.field_Private_List_1_Byte_0.Add(b);
            foreach (byte b in Encoding.ASCII.GetBytes(message))                          // Message Bytes
                packet.field_Private_List_1_Byte_0.Add(b);
            foreach (byte b in bytes)                                                     // Bytes
                packet.field_Private_List_1_Byte_0.Add(b);

            foreach (ulong clientId in clientIds)
                SteamPacketManager.SendPacket(new(clientId), packet, 8, destination);
        }
        
        public delegate void ClientMessageHandler(byte[] bytes);
        public delegate void ServerMessageHandler(ulong clientId, byte[] bytes);
    }
}