# Publish a portable build which also includes Windows GTK dependencies.
# GTK libs should already be in the LocalAppData directory from GtkSharp's automated download.

# WINDOWS
#==========================================================================
$gtkdir="$env:LOCALAPPDATA\Gtk\3.24.24\"
$publishbasedir="bin\Release\Publish\"
$publishdir="$publishbasedir\LynnaLab-win64"

rm -r $publishdir

dotnet publish /p:PublishProfile=Properties\PublishProfiles\win-x64.pubxml

$gitversion=cat version.txt

# Copy over Windows GTK libraries. For other platforms (linux, mac) GTK must be pre-installed.
echo "Copying GTK libraries to release directory..."
cp -Path @("$gtkdir\*.dll", "$gtkdir\etc", "$gtkdir\gtk3-runtime", "$gtkdir\lib", "$gtkdir\share", "$gtkdir\ssl") `
   -Destination $publishdir -Force -Recurse
   
# Zip it
echo "Compressing the archive..."
Compress-Archive -LiteralPath $publishdir -DestinationPath $publishbasedir\LynnaLab-$gitversion-win64.zip -Force

# LINUX/PORTABLE
#==========================================================================
$publishdir="$publishbasedir\LynnaLab-portable"

rm -r $publishdir
dotnet publish /p:PublishProfile=Properties\PublishProfiles\portable.pubxml

# Zip it
echo "Compressing the archive..."
Compress-Archive -LiteralPath $publishdir -DestinationPath $publishbasedir\LynnaLab-$gitversion-mac-linux.zip -Force