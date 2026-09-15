# How LynnaLab works
If you're reading this, you at least have LynnaLab running! But LynnaLab is only one component of a larger system.

The core of Oracles ROMhacking is actually [oracles-disasm](https://github.com/Stewmath/oracles-disasm/), or the Oracles disassembly. This exposes the Oracle games' code and data in human-readable form; it's the result of a years-long effort to document their inner workings.

LynnaLab does not modify ROMs; it modifies oracles-disasm. This means that even after you save your changes, oracles-disasm must be reassembled in order to produce a working ROM. This is done by a separate program called WLA-DX, but you don't need to worry about this; once everything is set up, all you have to do is click the "run" button in LynnaLab to trigger this process.

If you haven't already, see the setup instructions to get oracles-disasm into a functional state:
  * [Youtube: LynnaLab + oracles-disasm setup instructions](https://www.youtube.com/watch?v=u-Sz1d0ZdEk&t=128s), OR
  * The [LynnaLab readme](https://github.com/Stewmath/LynnaLab#setting-it-up).
