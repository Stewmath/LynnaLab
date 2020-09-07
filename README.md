An editor for Oracle of Ages (Seasons only partly supported).
[See the wiki](https://wiki.zeldahacking.net/oracle/LynnaLab) for additional information.

## Running it

Dependencies are:

* [.NET Core 3.1 Runtime](https://dotnet.microsoft.com/download/dotnet-core/3.1)

* GTK (64-bit windows library comes bundled with releases)

The releases are portable. On Windows you can simply run the ".exe" after installing .NET Core. On
Mac or Linux, you need to install GTK in addition to .NET Core, then execute this command to run
LynnaLab:

```
dotnet LynnaLab.dll
```

## Seasons support

This isn't really tested or supported yet, but Seasons kinda works. There is no seasons selector and
it only works on the disassembly's "master" branch, not the "hack-base" branch.

To switch between editing Ages or Seasons, open the "LynnaLab/config.yaml" file in the
oracles-disasm repository, and change the "EditingGame" field to "ages" or "seasons".
