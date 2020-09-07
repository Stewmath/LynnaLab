# Publish windows build.
# GTK libs should already be in the LocalAppData directory from GtkSharp's automated download.

dotnet publish /p:PublishProfile=Properties\PublishProfiles\win-x64.pubxml

$gtkdir="$env:LOCALAPPDATA\Gtk\3.24.20\"
$publishbasedir="bin\Release\Publish\"
$publishdir="$publishbasedir\LynnaLab-win64"

cp -Path @("$gtkdir\*.dll", "$gtkdir\etc", "$gtkdir\gtk3-runtime", "$gtkdir\lib", "$gtkdir\share", "$gtkdir\ssl") `
   -Destination $publishdir -Force -Recurse
Compress-Archive -LiteralPath $publishdir -DestinationPath $publishbasedir\LynnaLab-win64.zip -Force