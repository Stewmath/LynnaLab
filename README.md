An editor for The Legend of Zelda: Oracle of Ages & Seasons, based on
[oracles-disasm](https://github.com/stewmath/oracles-disasm).

See the [release trailer](https://www.youtube.com/watch?v=u-Sz1d0ZdEk) for a feature overview and
setup instructions!

![](images/preview-general.png)

## Features

### Easy room layout editing

You can edit a room by drawing a single tile at the desired position, or copying a range of tiles at
a time.

![](images/preview-brush.gif)

### Quickstart & Run

Press the quickstart icon to set where Link should spawn in, then click the Run
button to launch your emulator. The game will immediately load into the
specified room.

![](images/preview-quickspawn-1.png)
![](images/preview-quickspawn-2.png)

Note: You must have save data in file slot 1 for this to work, otherwise Weird
Things will happen.

### Object sprite previews

Sourced directly from the game's data files!

![](images/preview-objects.png)

### Convenient warp editing interface

Right-click on a warp to follow it to its destination, or interactively edit its
destination.

![](images/preview-warps.png)

### Networking

Collaborate with friends by starting a LynnaLab server and having clients connect to it, allowing
multiple people to edit data simultaneously. See [setup
instructions](https://wiki.zeldahacking.net/oracle/LynnaLab/Networking).

![](images/preview-networking.png)

### ...And much more

- Chest editing
- Tileset editor & cloner tools
- Dungeon editor
- Scratchpad window
- Undo/redo functionality
- Documentation for most objects

On top of all of this, LynnaLab is built on top of the
[oracles disassembly](https://github.com/stewmath/oracles-disasm), making the
entirety of the game's codebase available to modify as you please. Custom events
can be created using the game's native scripting language paired with assembly
code.

## Setting it up

### Windows

Either follow the steps in the [release trailer](https://www.youtube.com/watch?v=u-Sz1d0ZdEk&t=126s)
or follow the steps below.

The following steps are necessary to install not only LynnaLab, but also
oracles-disasm and the dependencies necessary to build it.

- Download and install [MSYS2](https://www.msys2.org/) with default settings.
- Download the [latest release](https://github.com/Stewmath/LynnaLab/releases) of LynnaLab.
- Extract the archive.
- Double-click on the "windows-setup.bat" file.
  - This will install some dependencies and download
    [oracles-disasm](https://github.com/stewmath/oracles-disasm) to your MSYS
    folder at `C:\msys64\home\<username>\oracles-disasm.`
- Launch LynnaLab.exe. This should automatically attempt to open oracles-disasm
  which was downloaded during the previous step.
- Configure your emulator path at File -> Choose Emulator Executable to be able
  to run the game.

### Linux

The dependencies on Linux are the .NET Core runtime and GTK3, in addition to the
dependencies for [oracles-disasm](https://github.com/stewmath/oracles-disasm).
You can run LynnaLab with the following command:

```
dotnet LynnaLab.dll
```

You may optionally specify the path to the disassembly and which game to edit:

```
dotnet LynnaLab.dll ~/oracles-disasm seasons
```

## Version numbering scheme (tentative) & oracles-disasm compatibility

LynnaLab modifies oracles-disasm, or more specifically the hack-base branch of
oracles-disasm in most cases. As existing projects cannot grab the latest
changes to the hack-base branch without a git merge operation (whose complexity
depends on the specific changes that have been made), I am experimenting with a
version numbering scheme to help keep track of which LynnaLab versions are
compatible with which hack-base versions.

LynnaLab has a 3-number versioning scheme: X.Y.Z (ie. 2.0.1).

X: The major version. This number will be updated when incompatible changes to
the disassembly's hack-base branch have occurred. Migrating projects between
major versions will require major intervention involving a git merge operation.
So, LynnaLab 1.0.0 projects will not be compatible with 2.0.0+ without manual
intervention.

Y: The minor version. New features may be added in these versions while
remaining compatible with existing projects with the same major version.
Projects may still benefit from updates to the hack-base branch, but this is
optional.

Z: The bugfix version. There shouldn't be any major changes to functionality in
these versions. Obviously there should be no incompatibilities introduced
either.

## Other stuff to know

* Logs are stored in the disassembly folder under the "LynnaLab\\Logs\\"
  subdirectory.
  
## Controls

### Global hotkeys

* F4: Toggle quickstart
* F5: Build & run game
* Ctrl-Z: Undo
* Ctrl-Shift-Z: Redo
* Ctrl+Tab: Cycle through windows

### Tileset viewer (left)

* Left/right click: Select a tile (drag to select a range of tiles)

### Room layout (middle)

* Left click: Place tile(s)
* Right click: Select a tile (drag to select a range of tiles)
* Ctrl + left click + drag: Rectangle fill with selected tile(s)
* Left click (on object): Select, drag object
  * Hold Ctrl to disable tile snapping
* Right click (on object): Open context menu
* Double click (on warp): Follow warp
* Delete key: Delete selected object, if any

### Minimap (right)

* Left click: Select a room to edit
* Middle click + drag: Pan through overworld (when zoomed in)
* Scroll wheel:
  * Zoom in & out (if "scroll to zoom" is ON)
  * Scroll vertically (if "scroll to zoom" is OFF)
    * Hold shift to scroll horizontally. (Can also apply to other windows that are horizontally scrollable)
    
### Tileset editor

* All modes:
  * Scroll on tileset: Zoom in
  * Middle click + drag: Pan through tileset (when zoomed in)
  * Palettes:
    * Right-click: Open copy/paste menu
    * Can also drag one palette onto another to copy it
* All brush modes (palette brush, subtile brush, collision brush):
  * Hover over the tileset and press a number key to toggle tile properties:
    * 1: Toggle Flip X
    * 2: Toggle Flip Y
    * 3: Toggle Priority
* Palette brush mode
  * Use this mode to assign palettes.
  * Right-click on tileset: Copy palette
  * Left-click on tileset: Assign palettes (hold Ctrl for rectangle fill)
* Subtile brush mode
  * Use this mode to set the subtile assignments in the tileset.
  * Right-click on tileset or subtile viewer: Copy subtile (right click+drag to copy range)
  * Left-click on tileset or tile preview: Assign subtiles (hold Ctrl for rectangle fill)
* Collision brush mode
  * Use this mode to set collisions.
  * Right-click on tileset or tile preview: Disable collision
  * Left-click on tileset or tile preview: Enable collision
  * Use Ctrl for rectangle fill in both cases
* Selection mode
  * Left-click on tileset: Select tile to inspect in the tile preview (bottom-right)
  * Left-click on tile preview: Select a subtile to inspect

## For more information

- Join our [discord server](https://discord.gg/wCpPPNZ) to meet some cool people
  who are into Zelda Oracles hacking!
- The [Zeldahacking wiki](https://wiki.zeldahacking.net/oracle/LynnaLab) has
  additional information on LynnaLab and Zelda Oracles hacking in general.
