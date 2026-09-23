# Emulator tab: General
This tab exposes a miscellaneous assortment of useful stuff for debugging.

## Variables
A few variables are exposed at the top. They are defined in [disasm:include/wram.s]().

**wLinkHealth**: Link's health in units of quarter hearts.

**wNumEnemies**: Number of enemies on the screen, used for the purpose of tracking "kill all enemies" puzzles. Generally, when this reaches 0, it triggers doors to open, etc.
  * Most enemies decrement this variable when they are killed, but this depends on the enemy (ie. sparks do not).
  * The "Uncounted" checkbox in the [Object Editor](RoomEditor-Objects.md) prevents enemies from increasing this variable when they are spawned.

**wNumRupees**, **wNumOreChunks**: Fairly self-explanitory. For the technically curious, these are hexadecimal numbers, but they are stored in BCD (binary-coded decimal) form. This is why they don't (or should never) have hexadecimal digits.

**wActiveRing**: Current equipped ring, or FF for no ring.

## Audio
You can play any music or sound effect here. I don't think I need to explain this!

## Room
This section shows the room flags for the current room. Each room has exactly 8 flags. Most important is ROOMFLAG_ITEM, which remembers if you've opened a chest or obtained an item in the room; other room flags can be used to remember opened keydoors, completed events, or literally anything else.

For more information, see: [Wiki: Room Flags](https://wiki.zeldahacking.net/oracle/Room_flags).

## Dungeon
If you are currently in a dungeon, you can adjust the items you have in this dungeon here.

You can also click the "reset dungeon" button, which will reset the dungeon into a pristine state by:
  * Clearing the room flags in every room in the dungeon.
  * Taking away the boss key, map, compass, and any small keys you have.

"Reset dungeon" works by checking the project's dungeon layout in LynnaLab. If you modified the dungeon after launching the emulator, the room flags that it clears will correspond to the layout in LynnaLab, not in the emulated game.

Some additional variables are also shown in this section (even if outside of a dungeon):

  * wActiveTriggers: This typically corresponds to pressed buttons. This resets to 0 when you change rooms.
  * wSwitchState: This corresponds to switch states. This value persists between rooms in dungeons.
  * wSpinnerState: The state of rotating spinners in the dungeon.

All of these values are treated as bitsets, that is, they are byte values where each individual bit can be used to manage a different button, switch, or spinner state.
