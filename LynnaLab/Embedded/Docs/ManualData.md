# Manual Data Editing
The following data in oracles-disasm currently has no LynnaLab interface, and must be edited by hand.

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

## Underwater surface data (ages-only)
To specify where on the screen you can rise above water.

[disasm:data/ages/underwaterSurfaceData.s]()

## Tree warps
Setting the tree warp position:

[disasm:data/{game}/treeWarps.s]()

Setting some extra data to make seeds respawn:

[disasm:data/{game}/seedTreeRefillData.s]()
