#!/usr/bin/env bash
#
# Run this script to generate distributable builds in the bin/Release/Publish directory. Obviously
# this is a bash script so it works best on Linux.

projectdir="$PWD"
projectfile="$projectdir/LynnaLab.csproj"
publishbasedir="$projectdir/bin/Release/Publish"
profiledir="$projectdir/Properties/PublishProfiles"
versionfile="$projectdir/../LynnaLib/version.txt"

# WINDOWS
#==========================================================================
publishdirname="LynnaLab-win64"

mkdir -p "$publishbasedir"
cd "$publishbasedir"
rm -R "$publishdirname/"

dotnet publish "$projectfile" /p:PublishProfile="$profiledir/win-x64.pubxml"

gitversion=$(cat "$versionfile")
zipname="$publishbasedir/LynnaLab-$gitversion-win64.zip"

# Zip it
echo "Compressing the archive..."
rm "$zipname"
zip -q -r "$zipname" "$publishdirname/"

# LINUX/PORTABLE
#==========================================================================
publishdirname="LynnaLab-portable"

rm -R "$publishdirname/"
dotnet publish "$projectfile" /p:PublishProfile="$profiledir/portable.pubxml"

# Remove unneeded platform libraries
rm -R \
  "$publishdirname/runtimes/android-arm" \
  "$publishdirname/runtimes/android-arm64" \
  "$publishdirname/runtimes/android-x64" \
  "$publishdirname/runtimes/android-x86" \
  "$publishdirname/runtimes/ios"

gitversion=$(cat "$versionfile")
zipname="$publishbasedir/LynnaLab-$gitversion-mac-linux.zip"

# Zip it
echo "Compressing the archive..."
cd "$publishbasedir"
rm "$zipname"
zip -q -r "$zipname" "$publishdirname/"
