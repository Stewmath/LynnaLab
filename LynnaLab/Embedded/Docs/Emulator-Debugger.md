# Emulator tab: Debugger
This tab exposes SameBoy's built-in debugger CLI interface.

## Cheatsheet
  * General:
    * help: Print general help
    * help dis: Help for "dis" command
  * Memory:
    * p [wLinkHealth]: Print an 8-bit variable
    * p {wNumRupees}: Print a 16-bit variable
    * p [wLinkHealth] = $0c: Write a byte value
    * p a = $8c: Set a register value
  * Breakpoints:
    * l: List breakpoints
    * b playSound: Breakpoint on function "playSound"
    * de 1: Delete a breakpoint
    * w/r wActiveRing: Read watchpoint on 8-bit variable
    * w/w wNumBombs: Write watchpoint on 8-bit variable
    * unw 1: Delete a watchpoint
  * Code examination
    * r: Print "registers"
    * dis/10 playSound: Disassemble 10 instructions at start of function "playSound"
    * bt: Backtrace
  * Code flow:
    * i: Pause execution ("interrupt")
    * c: Resume execution ("continue")
    * n: Run next instruction, skipping function calls ("next")
    * s: Run next instruction, stepping into function calls ("step")
    * f: Run to end of function ("finish")

## For more information
For more complete documentation, see: [https://sameboy.github.io/debugger/]()

TODO:
- Move help text into here
