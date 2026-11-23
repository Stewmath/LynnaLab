# Manual Data Editing
The following data in oracles-disasm currently has no LynnaLab interface, and must be edited by hand.

Since LynnaLab doesn't use most of the files listed here, you can edit them freely. But if you choose to edit a file that LynnaLab **does** read or modify, LynnaLab will not see those changes until you reload your project. (PNG files are the only exception; they are reloaded automatically.)

When editing data manually, use "Shift+F5" to force a rebuild when running the game. Otherwise, a rebuild will only occur if something was modified in LynnaLab.

## Text
Very straightforward to modify manually.

[disasm:text/{game}/text.yaml]()

## Static Dungeon Objects
Mainly used for minecarts (objects which remember their position between rooms within a dungeon).

[disasm:data/{game}/staticDungeonObjects.s]()

## Room-specific tile changes
For changes to rooms reacting to events.

  * [disasm:data/{game}/singleTileChanges.s](): Simple changes (lifted a rock)
  * [disasm:code/{game}/roomSpecificTileChanges.s](): Complex changes (destroyed Moblin's keep)

## Sign text
[disasm:data/{game}/signText.s]()

## Rooms in alt world
An alternative way to mark indoor rooms as being in the past (or subrosia) without using a specific tileset for that.

[disasm:data/{game}/roomsInAltWorld.s]()

## Map popups
The Region Editor can handle map text, but not map popups.

[disasm:data/{game}/mapTextAndPopups.s]()

## Palette transitions
For smooth transitions between areas, ie. entering Yoll Graveyard.

[disasm:data/{game}/paletteTransitions.s]()

## Tree warps
Setting the tree warp position:

[disasm:data/{game}/treeWarps.s]()

Setting some extra data to make seeds respawn:

[disasm:data/{game}/seedTreeRefillData.s]()

## Underwater surface data (ages-only)
To specify where on the screen you can rise above water.

[disasm:data/ages/underwaterSurfaceData.s]()

## Season assignments (seasons-only)
Each byte corresponds to a specific room pack.

[disasm:data/seasons/roomPackSeasonTable.s]()

## Tile properties
While basic collisions are modified in the [tileset editor](TilesetEditor.md), more complex properties must be modified by hand in the tile_properties folder.

[disasm:data/{game}/tile_properties/]()
