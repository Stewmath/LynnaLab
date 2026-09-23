# Emulator tab: Memory
View and modify the memory of the emulated gameboy in real-time.

## Controls
  * Click on a byte and begin typing to change its value.
  * Press Ctrl+G (after clicking to focus the hex editor) and enter an address (or variable name) to jump to that location.
    * IE. "c6aa" to jump to that address; "wLinkHealth" to jump to the address of the wLinkHealth variable.
    * Any variable defined in the symbol file should be supported (most WRAM variables, but not HRAM at the moment)

## Memory types
**System Bus**: Use this most of the time. This is the "normal" gameboy address space; addresses in the disassembly match up with this.

**WRAM**: Exposes all 8 banks of WRAM in sequential order, IE. bank 0 starts at 0000, bank 1 at 1000, etc.

There is one reason why you might need to use WRAM instead of the System Bus: that is, the System Bus tab does not support banking at all. It always exposes:
  * ROM banks 0-1
  * VRAM bank 0
  * SRAM bank 0
  * WRAM banks 0-1

This should be fine for 90% of most people's needs, as usually the only thing you'll care about is WRAM bank 0 (addresses c000-cfff), and maybe HRAM (ff80-fffe), as this is where the game's most important variables are stored.

However, if you need to access WRAM banks 2-7, then you need to use the WRAM memory type for that. Ctrl+G still works as long as the variable name you enter is actually in WRAM.

## Variable names
All variables are documented in:

  * [disasm:include/wram.s]()
  * [disasm:include/hram.s]()

Note! HRAM addresses are not exposed in the symbol file, so when using Ctrl+G, you will need to enter their addresses by hand, instead of their names. (Hopefully this will be rectified later!)

## Jump-to-address menu
Note: The Ctrl+G jump menu takes hexadecimal numbers for its address. However, when parsing something that is not a number, the expression is passed to SameBoy's evaluator, which defaults to decimal.

Example:
  * "10"          -> raw number        -> jump to address 0x10
  * "10+1"        -> SameBoy evaluator -> jump to address 0x0b (11 in decimal)
  * "$10+1"       -> SameBoy evaluator -> jump to address 0x11
  * "wLinkHealth" -> SameBoy evaluator -> jump to address that wLinkHealth evaluates to

Sorry for the weirdness!
