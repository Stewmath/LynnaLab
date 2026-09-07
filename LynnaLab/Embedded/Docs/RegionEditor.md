# Region Editor
The region editor lets you edit per-room data such as tileset and music assignments without needing to select each room individually.

## Tile Edit Mode
The controls in this mode are much like in the room editor:

  * Right click on the map to select a value (ie. tileset index).
    * You can also manually select a value to use in the left pane.
  * Left-click on the map to paste that value.
  * Ctrl+Left-click on the map to fill a rectangular region with the selected value.

## Bulk Edit Mode
In this mode, clicking on the map does not assign a new value, instead it selects a region. You can then edit the value in the left pane to change the value for the entire region at once.

Note: Changing a value in bulk edit mode does not automatically cause the map to be redrawn / recolored, unlike in tile edit mode, in order to prevent regions being abruptly merged together when their values become equal. This behaviour is generally easier to work with, but it's quite weird when editing Maple spawns, so in that case just stick with tile edit mode.

## Maple spawns
Unlike tilesets/music/etc, maple spawning is a binary, on/off thing. It works the same way as everything else, but the "Spawns" checkbox takes the place of the value entry in the left pane. When in tile edit mode, left-clicking will allow or prevent Maple from spawning, depending on the checkbox value.

## For more information
[Wiki: Region Editor](https://wiki.zeldahacking.net/oracle/LynnaLab/Region_editor)
