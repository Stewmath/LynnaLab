REM     Because GTK's file chooser seems to have no way to locate WSL files, this batch script can be used
REM     to feed the path to the disassembly directly to LynnaLab.
REM     Type "explorer ." in a ubuntu terminal to open explorer, then press "Ctrl-L" to get the path to that folder.
REM     Copy that path here (replace everything after "LynnaLab.exe").
LynnaLab.exe \\wsl$\Ubuntu\home\<REPLACE WITH LINUX USERNAME>\oracles-disasm