# Dungeon Editor
This lets you edit the layout and properties of dungeons.

## Dungeon Properties
  * Group: The group number from which the dungeon gets its rooms. Changing this changes ALL rooms in the dungeon.
  * Wallmaster dest room: All wallmasters in the dungeon send you to this room.
  * Base floor name: Determines what the game will call the bottom floor.
    * $00: The bottom floor is 'B3'.
    * $01: The bottom floor is 'B2'.
    * $02: The bottom floor is 'B1'.
    * $03: The bottom floor is 'F1'.
  * Floors unlocked with compass: A bitset of floors that will be shown on the map when the compass is obtained. This should include all floors in the dungeon.
    * You'll need to do some bit math on this! IE. Value $05 means bits 0 and 2 are set (floors 0 and 2 are unlocked).
  * You can freely add & remove floors with the buttons available. Staircases will automatically send you to the corresponding room on the next floor even with no warps defined (but if there is a warp, that takes precedence).

## Room properties
These affect the current selected room.

  * Room: The room assigned to the current selected position on the minimap. Adjust this value to rearrange the dungeon layout.
  * Up/Down/Left/Right: Whether the maps shows connections in this direction from this room. Note, bombable walls and keydoors opened in these directions automatically create links even if they are not defined here.
  * Key: Whether a key exists in this room (compass chime plays).
  * Chest: Whether to show a chest on the map here.
  * Boss: Whether the boss room should be shown on the map here.
  * Dark: Whether this room is dark (rooms which typically have lightable torches).
  * NOTE: Set both "Chest" and "Boss" to hide the room so that it is not displayed on the in-game map (mainly for sidescrolling areas).
