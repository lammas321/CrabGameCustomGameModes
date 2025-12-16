using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CustomGameModes.CustomGameModes;

namespace CustomGameModes
{
    public static class Api
    {
        internal static Dictionary<string, CustomGameMode> customGameModes = [];

        public static int VanillaGameModesCount { get; internal set; } = -1;
        public static int SkippedGameModesCount { get; internal set; } = -1;
        
        internal static CustomGameMode currentCustomGameMode = null;
        
        public static bool RegisterCustomGameMode(CustomGameMode customGameMode)
        {
            if (
                VanillaGameModesCount != -1 ||
                customGameModes.ContainsKey(customGameMode.Name) ||
                customGameModes.ContainsValue(customGameMode) ||

                !Enum.IsDefined(typeof(GameModeData_GameModeType), customGameMode.GameModeType) ||
                customGameMode.GameModeType == GameModeData_GameModeType.Waiting ||
                customGameMode.GameModeType == GameModeData_GameModeType.Practice ||

                !Enum.IsDefined(typeof(GameModeData_GameModeType), customGameMode.VanillaGameModeType) ||
                customGameMode.VanillaGameModeType == GameModeData_GameModeType.Waiting ||
                customGameMode.VanillaGameModeType == GameModeData_GameModeType.Practice ||
                customGameMode.VanillaGameModeType == GameModeData_GameModeType.Baseball ||
                customGameMode.VanillaGameModeType == GameModeData_GameModeType.Standoff
            )
                return false;

            customGameModes.Add(customGameMode.Name, customGameMode);
            return true;
        }

        internal static void AddCustomGameModes()
        {
            if (VanillaGameModesCount != -1)
                return;
            VanillaGameModesCount = GameModeManager.Instance.allGameModes.Count;

            LinkedList<GameModeData> gameModeDatas = new(GameModeManager.Instance.allGameModes);
            HashSet<string> vanillaGameModeNames = [.. GameModeManager.Instance.allGameModes.Select(gameModeData => gameModeData.modeName)];
            int nextGameModeId = VanillaGameModesCount;
            foreach (CustomGameMode customGameMode in customGameModes.Values.ToArray())
            {
                if (vanillaGameModeNames.Contains(customGameMode.Name))
                {
                    customGameModes.Remove(customGameMode.Name);
                    continue;
                }

                GameModeData customGameModeData = ScriptableObject.CreateInstance<GameModeData>();
                customGameMode.GameModeId = nextGameModeId++;
                customGameModeData.id = customGameMode.GameModeId;
                customGameModeData.name = customGameMode.Name;
                customGameModeData.modeName = customGameMode.Name;
                customGameModeData.modeDescription = customGameMode.Description;

                customGameModeData.musicType = customGameMode.MusicType;
                customGameModeData.waitForRoundOverToDeclareSoloWinner = customGameMode.WaitForRoundOverToDeclareSoloWinner;

                customGameModeData.type = customGameMode.GameModeType;

                customGameModeData.minPlayers = customGameMode.MinPlayers;
                customGameModeData.maxPlayers = customGameMode.MaxPlayers;

                customGameModeData.shortModeTime = customGameMode.ShortModeTime;
                customGameModeData.mediumModeTime = customGameMode.MediumModeTime;
                customGameModeData.longModeTime = customGameMode.LongModeTime;

                customGameModeData.smallMapsOnlyPlayers = customGameMode.SmallMapPlayers;
                customGameModeData.mediumAndSmallMapPlayers = customGameMode.MediumAndSmallMapPlayers;
                customGameModeData.bigAndMediumMapPlayers = customGameMode.LargeAndMediumMapPlayers;
                customGameModeData.bigOnlyMapPlayers = customGameMode.LargeMapPlayers;

                customGameModeData.isPlayable = true;
                customGameModeData.skipAsString = false;
                customGameModeData.modeTime = 20;
                customGameModeData.smallMaps = new(0);
                customGameModeData.mediumMaps = new(0);
                customGameModeData.largeMaps = new(0);

                foreach (GameModeData gameModeData in GameModeManager.Instance.allGameModes)
                    if (gameModeData.id < VanillaGameModesCount && gameModeData.type == customGameMode.VanillaGameModeType)
                    {
                        customGameMode.VanillaGameModeId = gameModeData.id;
                        break;
                    }

                if (customGameMode.PreloadMapName != null)
                    foreach (Map map in MapManager.Instance.maps)
                        if (map.mapName == customGameMode.PreloadMapName)
                        {
                            customGameMode.PreloadMapId = map.id;
                            break;
                        }


                LinkedList<string> compatibleMapNames = new(customGameMode.CompatibleMapNames);
                LinkedList<Map> compatibleMaps = [];
                foreach (Map map in MapManager.Instance.maps)
                {
                    LinkedListNode<string> indexNode = null;
                    for (LinkedListNode<string> node = compatibleMapNames.First; node != null; node = node.Next)
                        if (node.Value == map.mapName)
                        {
                            indexNode = node;
                            break;
                        }

                    if (indexNode == null)
                        continue;

                    compatibleMapNames.Remove(indexNode);
                    compatibleMaps.AddLast(map);
                }

                foreach (string mapName in compatibleMapNames)
                    Instance.Log.LogWarning($"Could not find a map by the name of '{mapName}' for the custom game mode '{customGameModeData.modeName}'.");

                if (compatibleMaps.Count == 0)
                {
                    Instance.Log.LogWarning($"The custom game mode '{customGameModeData.modeName}' does not have any compatible maps, giving it the map '{MapManager.Instance.GetMap(0).mapName}'.");
                    compatibleMaps.AddLast(MapManager.Instance.GetMap(0));
                }
                customGameModeData.compatibleMaps = compatibleMaps.ToArray();


                gameModeDatas.AddLast(customGameModeData);
                GameModeManager.Instance.allPlayableGameModes.Add(customGameModeData);
                if (customGameMode.IsSmallMode)
                    GameLoop.Instance.smallModes.Add(customGameModeData);
            }

            SkippedGameModesCount = 0;
            foreach (GameModeData gameModeData in gameModeDatas)
                if (gameModeData.skipAsString)
                    SkippedGameModesCount++;

            GameModeManager.Instance.allGameModes = gameModeDatas.ToArray();
        }
    }

    public abstract class CustomGameMode
    (
        string name,
        string description,
        GameModeData_GameModeType gameModeType,
        GameModeData_GameModeType vanillaGameModeType,
        string preloadMapName = null,

        bool isSmallMode = true,
        MusicController_SongType musicType = MusicController_SongType.Intense,
        bool waitForRoundOverToDeclareSoloWinner = false,

        int minPlayers = 0,
        int maxPlayers = int.MaxValue,

        int shortModeTime = 60,
        int mediumModeTime = 90,
        int longModeTime = 120,

        IEnumerable<string> compatibleMapNames = null,
        int smallMapPlayers = 4,
        int mediumAndSmallMapPlayers = 5,
        int largeAndMediumMapPlayers = 9,
        int largeMapPlayers = 100
    )
    {
        public string Name { get; internal set; } = name;
        public string Description { get; internal set; } = description;
        public GameModeData_GameModeType GameModeType { get; internal set; } = gameModeType;               // The game mode to be used when using the mod adding this custom game mode
        public GameModeData_GameModeType VanillaGameModeType { get; internal set; } = vanillaGameModeType; // The game mode to be used when NOT using the mod adding this custom game mode
        public string PreloadMapName { get; internal set; } = preloadMapName;                 // The name of the map to load before loading into the proper map. Don't use this unless you specifically need it, such as to access teams from Tile Drive maps
        public string Version { get; protected set; } = "0.0.0"; 
        public int GameModeId { get; internal set; } = -1;
        public int VanillaGameModeId { get; internal set; } = -1;
        public int PreloadMapId { get; internal set; } = -1;
        
        public bool IsSmallMode { get; internal set; } = isSmallMode; // Is playable with just 2 players
        public MusicController_SongType MusicType { get; internal set; } = musicType; // The type of music to play
        public bool WaitForRoundOverToDeclareSoloWinner { get; internal set; } = waitForRoundOverToDeclareSoloWinner; // Should the game wait for the round timer to finish at 1 player remaining (true) or should it end automatically if only one player is left alive (false)?

        public int MinPlayers { get; internal set; } = minPlayers; // The minimum number of players required to play this game mode
        public int MaxPlayers { get; internal set; } = maxPlayers; // The maximum number of players that can play this game mode at once

        public int ShortModeTime { get; internal set; }  = shortModeTime;   // Players <= 4
        public int MediumModeTime { get; internal set; } = mediumModeTime;  // Players > 4  and  Players < 10
        public int LongModeTime { get; internal set; }   = longModeTime;    // Players >= 10

        public string[] CompatibleMapNames { get; internal set; }  = [.. compatibleMapNames ?? []];
        public int SmallMapPlayers { get; internal set; }          = smallMapPlayers;            // Players <= smallMapPlayers
        public int MediumAndSmallMapPlayers { get; internal set; } = mediumAndSmallMapPlayers;   // Players >= mediumAndSmallMapPlayers  and  Players < largeAndMediumMapPlayers
        public int LargeAndMediumMapPlayers { get; internal set; } = largeAndMediumMapPlayers;   // Players >= largeAndMediumMapPlayers  and  Players < largeMapPlayers
        public int LargeMapPlayers { get; internal set; }          = largeMapPlayers;            // Players >= largeMapPlayers

        public Dictionary<ulong, int> ClientsWithGameMode { get; internal set; } = [];
        public Dictionary<ulong, string> ClientGameModeVersions { get; internal set; } = [];
        
        public virtual void PreInit() { }
        public virtual void PostEnd() { }
    }
}