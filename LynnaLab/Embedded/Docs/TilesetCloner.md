# Tileset Cloner
The Tileset Cloner allows you to copy some or all properties of one tileset to another tileset.

The left tileset is the source; the right tileset is the destination. No edits are applied until the "Apply Tileset Changes" button is clicked.

The "Tileset Properties" pane on the bottom applies to the destination tileset. These are the same tileset properties seen in the [tileset editor](TilesetEditor.md). Technically you can also edit them here in the cloner, though their real purpose is to help you to see the data making up the destination tileset.

## Data to copy
  * Graphics: Copy the raw graphics (PNG file) from source to destination.
  * Tilemap/flags: Copy the tilemap (which tiles to use) and flags (which palettes to use + flip properties) to the destination.
  * Collisions: Copy collision data to the destination. (Only affects basic solidity, not other properties like warps, etc)
  * Properties: Copy the source tileset's properties to the destination. (You'll see the properties in the bottom pane change to illustrate this change.)
