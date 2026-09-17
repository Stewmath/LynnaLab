# Tileset Editor
This is split into three main components: Tileset Viewer (left), Subtile Viewer (top-right), and Tile Preview (bottom-right).

You'll also see Tileset Properties (expandable) on the bottom and palettes on the right side. Palettes are not unique per-tileset, rather they depend on the value of the "Palettes" property.

You can use the scroll wheel to zoom into the tileset viewer a bit further if Scroll To Zoom is enabled.

## Controls
  * Hover over a subtile and press the "1", "2", or "3" keys to toggle the Flip X, Flip Y, and Priority properties, respectively.
    * This always works in the tile preview. In brush modes, this also works in the tileset viewer.
  * Palettes: Left-click to open a color picker, right-click to copy or paste. The value is copied to the system clipboard, so can be shared between LynnaLab instances.

### Subtile brush mode
  * Use this mode to set the subtile assignments in the tileset.
  * Right-click on tileset or subtile viewer: Copy subtile (right click+drag to copy range)
  * Left-click on tileset or tile preview: Assign subtiles (hold Ctrl for rectangle fill)

### Palette brush mode
  * Use this mode to assign palettes.
  * Right-click on tileset: Copy palette
  * Left-click on tileset: Assign palettes (hold Ctrl for rectangle fill)

### Collision brush mode
  * Use this mode to set collisions.
  * Right-click on tileset or tile preview: Disable collision
  * Left-click on tileset or tile preview: Enable collision
  * Use Ctrl for rectangle fill in both cases.
  * Tiles colored blue have a nonstandard collision (value is greater than 0F). Edit the value manually in the tile preview.
    * Nonstandard values are used by hazards (value 10 for holes/water), but it's only to prevent enemy AI from spawning or moving there, probably.
    * Other nonstandard values are used by bridges.
  * For more complex tile collision properties, see: [disasm:data/{game}/tile_properties/]().
    * These tables are not unique per-tileset (they use one of the 6 "collision types" selectable in the tileset properties)

### Selection Mode
  * Left-click in the tileset viewer to select a tile.
  * Click on a subtile in the tile preview to select one to edit.
  * Use the property boxes that appear to modify its properties.
    * If no subtile is selected, you can see and edit the collision value for the tile.

## Palettes
Palette 0 is fixed (for hearts on the HUD), but can be used by the tileset. Palette 1 is reserved for textboxes and cannot be used by the tileset.

A well-formed tileset palette should only set BG palettes 2-7. However it's possible to select palettes that don't follow this rule (even ones that set SPR palettes instead of BG!). Just stick with palettes named PALH_TILESET_[...].

The palette data is located in:
  * [disasm:data/{game}/paletteHeaders.s]()
  * [disasm:data/{game}/paletteData.s]()

If you're careful, you can edit this manually to add new palettes. (You must reload your project in LynnaLab after doing this.)

You cannot rename existing palette headers without doing a project-wide find/replace.
