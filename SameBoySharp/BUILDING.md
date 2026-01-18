LibSameBoy is a gameboy emulator library that I'm using to test gameplay live within a LynnaLab
instance. It lets me hack the internals as it's running, allowing for a high degree of integration
with LynnaLab!

Since it is written in C, it needs to be compiled separately from LynnaLab and packaged as a
library. To keep things simple, windows and linux builds are pre-packaged in the "deps" folder.

## Building on Linux

If you want to build it on your own for some reason, [https://github.com/LIJI32/SameBoy](Grab
the SameBoy source) and run "CONF=release make lib" to generate libsameboy.so.

## Building on Windows

The [https://github.com/LIJI32/SameBoy](SameBoy readme) indicates that the libsameboy target is
"currently not supported on Windows due to linker limitations".

It can be done though. The dll in this repo was cross-compiled on Linux using mingw with a few
makefile tweaks.

However, LTO is not enabled in this build, which could be problematic for performance, since the
author considers it a critical optimization. (Perhaps this was the aferomentioned linker
limitation?)
