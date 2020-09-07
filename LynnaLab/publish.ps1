# Publish a portable build which also includes Windows GTK dependencies.
# GTK libs should already be in the LocalAppData directory from GtkSharp's automated download.

# WINDOWS
#==========================================================================
dotnet publish /p:PublishProfile=Properties\PublishProfiles\portable.pubxml

$gtkdir="$env:LOCALAPPDATA\Gtk\3.24.20\"
$publishbasedir="bin\Release\Publish\"
$publishdir="$publishbasedir\LynnaLab"
$gitversion=cat version.txt

# Copy over Windows GTK libraries. For other platforms (linux, mac) GTK must be pre-installed.
echo "Copying GTK libraries to release directory..."
cp -Path @("$gtkdir\*.dll", "$gtkdir\etc", "$gtkdir\gtk3-runtime", "$gtkdir\lib", "$gtkdir\share", "$gtkdir\ssl") `
   -Destination $publishdir -Force -Recurse
   
# Zip it
echo "Compressing the archive..."
Compress-Archive -LiteralPath $publishdir -DestinationPath $publishbasedir\LynnaLab-$gitversion.zip -Force