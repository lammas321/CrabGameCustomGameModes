namespace CustomGameModes
{
    public sealed class CustomGameModeBaseball() : CustomGameMode
    (
        name: "Baseball",
        description: "• Baseball without balls, players must try to get other players out of bounds to eliminate them.",
        gameModeType: GameModeData_GameModeType.Baseball,
        vanillaGameModeType: GameModeData_GameModeType.FallingPlatforms,

        shortModeTime: 60,
        mediumModeTime: 75,
        longModeTime: 90,

        compatibleMapNames: [
            "Bitter Beach",
            "Cocky Containers",
            "Islands",
            "Lanky Lava",
            "Return to Monke",
            "Snowtop",
            "Toxic Train",
            "Mini Monke",
            "Small Beach",
            "Small Containers",
            "Sandy Islands",
            "Lava Dump",
            "Salty Island",
            "Lava Climb",
            "Macaroni Mountain",
            "Sussy Sandcastle",
            "Sussy Slope",
            "Crabfields",
            "Crabheat",
            "Crabland"
        ],
        smallMapPlayers: 3,
        mediumAndSmallMapPlayers: 4,
        largeAndMediumMapPlayers: 7,
        largeMapPlayers: 12
    );
}