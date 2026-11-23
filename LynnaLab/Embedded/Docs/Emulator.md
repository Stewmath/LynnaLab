# Internal Emulator
The [SameBoy](https://sameboy.github.io/) emulator is integrated into LynnaLab. By default, pressing F5 or the "run" button launches SameBoy in an internal LynnaLab window.

Alternatively, LynnaLab can be configured to launch an external emulator. This may be useful if another emulator has a debugging interface that you prefer (ie. [BGB](https://wiki.zeldahacking.net/oracle/BGB)).

This page describes the internal emulator only. However, [quickstart](Emulator-Quickstart.md) is supported on both internal and external emulators.

## Controls
Default emulator controls are:
  * Arrow keys for movement
  * Z=A
  * X=B
  * Enter=Start,
  * Right Shift=Select
  * Spacebar=Speedup

You can remap the controls in LynnaLab's settings.

## Debugging tabs
The internal emulator has numerous debugging capabilities, exposed through these tabs:

  * [General](Emulator-General.md)
  * [Object](Emulator-Object.md)
  * [Treasure](Emulator-Treasure.md)
  * [Global flags](Emulator-Globalflags.md)
  * [Debugger](Emulator-Debugger.md)
  * [Memory](Emulator-Memory.md)

## Save data
When a ROM is loaded, it reads "ages.sav" or "seasons.sav" from the disk in the same directory as the corresponding "ages.gbc" or "seasons.gbc" file. This is also where BGB and other emulators look for save data, so it will share save data with those emulators.

Save data is written back to disk whenever the game is saved. (Specifically, a breakpoint is placed on the "saveFile", "eraseFile", and "initializeFile" functions, and the .sav file is updated whenever the breakpoint is hit.)
