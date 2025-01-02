#!/usr/bin/env bash

# This should basically work the same as publish.ps1, but you'll need the
# windows gtk libs available an /opt/gtk-win on a linux system.
#
# This only exists because I don't want to boot into windows every time I
# publish a release. Should probably consolidate the two publish scripts
# somehow.

# WINDOWS
#==========================================================================
gtkdir="/opt/gtk-win64/3.24.24"
projectdir="$PWD"
projectfile="$projectdir/LynnaLab.csproj"
publishbasedir="$projectdir/bin/Release/Publish"
profiledir="$projectdir/Properties/PublishProfiles"
versionfile="$projectdir/version.txt"
publishdirname="LynnaLab-win64"

mkdir -p "$publishbasedir"
cd "$publishbasedir"
rm -R "$publishdirname/"

dotnet publish "$projectfile" /p:PublishProfile="$profiledir/win-x64.pubxml"

gitversion=$(cat "$versionfile")
zipname="$publishbasedir/LynnaLab-$gitversion-win64.zip"

# Copy over cairo library & associated dependencies from a windows GTK installation.
# (Would be nice to eliminate this dependency later.)
echo "Copying GTK libraries to release directory..."
cp "$gtkdir"/libcairo-2.dll "$gtkdir"/libgcc_s_seh-1.dll "$gtkdir"/libfontconfig-1.dll \
    "$gtkdir"/libfreetype-6.dll "$gtkdir"/libpixman-1-0.dll "$gtkdir"/libpng16-16.dll \
    "$gtkdir"/zlib1.dll "$publishdirname/"

# Zip it
echo "Compressing the archive..."
rm "$zipname"
zip -q -r "$zipname" "$publishdirname/"

# LINUX/PORTABLE
#==========================================================================
publishdirname="LynnaLab-portable"

rm -R "$publishdirname/"
dotnet publish "$projectfile" /p:PublishProfile="$profiledir/portable.pubxml"

gitversion=$(cat "$versionfile")
zipname="$publishbasedir/LynnaLab-$gitversion-mac-linux.zip"

# Zip it
echo "Compressing the archive..."
cd "$publishbasedir"
rm "$zipname"
zip -q -r "$zipname" "$publishdirname/"
