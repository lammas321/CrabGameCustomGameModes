# CrabGameCustomGameModes
A BepInEx mod for Crab Game that adds custom game modes in a vanilla compatible way.

## What does this add?
It offers modders an API of sorts for anyone that wants to create vanilla compatible custom game modes.
Any custom game modes that are added will be listed in the game creation menu and shown in the server list to anyone else with CustomGameModes.

To show off what CustomGameModes can do, it adds back two game modes that used to exist: Baseball and Standoff.

### Baseball
- Baseball without balls, players must try to get other players out of bounds to eliminate them.

Everyone is given bats and must hit other players out of the map.
This is actually a game mode that existed during Dani's development and play testing of Crab Game, before its first release.
You can actually see it for a couple of seconds in [the game's trailer at 0:23](https://youtu.be/eHsIvXz6ryc?si=kIlE3lV91Uj0ff8I&t=23) for 4 seconds.
However, the maps Haven and Icy Haven were never included in Dani's publicly released versions of Crab Game, so I made it use a bunch of other random maps.

### Standoff
- You can only shoot when signaled
- Hit someone to give them a "mark"
- Bullets left in your gun count as "marks"
- Most marked players are eliminated

Everyone are given revolvers with 6 shots (starting everyone with 6 "marks" aka penalties), and every couple of seconds the game will toggle between being able to shoot and being unable to.

Players that join you without the mod will...
- not see the signal vignette when they are able to shoot (because they're "technically" playing Hat King)
- not be able to hear any Standoff related sfx (same reason as above)
- see that they and everyone else have a negative Hat King score (because you want to have a low score, and your score would have a red background if you were technically "winning" with few penalties)
- and can see that they're in the red but still live (because I made the amount of players Standoff kills more forgiving and the amount of players that will die is calculated on every client)

## What are some other custom game modes I can get?
- ### [Squenced Drop](https://github.com/lammas321/CrabGameSequencedDropGameMode)
- ### [Unstable Rocks](https://github.com/lammas321/CrabGameUnstableRocksGameMode)
- ### [Tantan Prime](https://github.com/lammas321/CrabGameTantanPrimeGameMode)
