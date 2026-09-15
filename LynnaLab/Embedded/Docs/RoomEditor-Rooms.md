# Room Editor: Rooms
The "Room" tab of the room editor has two functions: Providing you with the tileset for editing the tiles in the room, and exposing some room-specific properties.

## Tile editing
Controls:
  * Click (& drag) on the tileset to select a tile, or a range of tiles.
  * Right click (& drag) on the room preview to copy tiles from the room.
  * Left click on the room preview to paste the tile(s) that were selected.
  * Ctrl + Left click on the room preview to do a rectangle fill.

## Room properties
Note: Most fields here are more easily modified through the region editor.

  * Tileset: The tileset index assigned to this room.
  * Music: What music to play in this room. (It takes a bit of doing, but custom music [is supported](https://wiki.zeldahacking.net/oracle/Custom_Music)!)
  * Room Pack:
    * Seasons: Separates sections of the map with different seasons, forcing a fadeout transition and season recalculation when you move between them. Each room pack index has a specific assigned season (see: [disasm:data/seasons/roomPackSeasonTable.s]()).
    * Ages: Mostly vestigial. But it is used to mark off the animal companion regions for special-case handling, as well as forcing a screen refresh when entering the maku tree screen.
  * Gfx Load After Transition: Determines when this room's tileset graphics are loaded; either **before** or **after** the screen moves. This can be important when transitioning between rooms with different tilesets.
