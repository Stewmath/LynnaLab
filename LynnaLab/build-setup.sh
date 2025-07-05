#!/bin/bash
#
# Run this script to:
# - Build WLA-DX v10.6 and copy it to /usr/local/bin
# - Clone oracles-disasm and checkout the hack-base branch
#
# When run on Windows though "windows-setup.bat" this should set up everything
# required to start using LynnaLab immediately. It should also work on Linux
# but you'll need to ensure that the necessary dependencies are installed (git,
# make, python, python-yaml, cmake, gcc), and you'll probably want to modify
# SETUP_DIR to avoid cluttering your home directory.
#
# LynnaLab is hardcoded to look for oracles-disasm at this location on windows,
# so don't change without a good reason.
SETUP_DIR=~


BOLD="\033[1;37m"
NC="\033[0m"

function heading
{
	echo
	echo -e "${BOLD}$@$NC"
}


cd "$SETUP_DIR"

if [[ $MSYSTEM == "UCRT64" ]]; then
	heading "Installing MSYS2 dependencies..."
	pacman -S --needed --noconfirm git make mingw-w64-ucrt-x86_64-python mingw-w64-ucrt-x86_64-python-yaml mingw-w64-ucrt-x86_64-cmake mingw-w64-ucrt-x86_64-gcc
fi

# Building wla-dx instead of downloading release from github because github
# version depends on some C runtime libraries that may not be installed by
# default. Anyway doing it this way allows the script to work on linux too.
if [[ -e /usr/local/bin/wla-gb ]]; then
	heading "Skipping wla-dx compilation (already in /usr/local/bin)."
else
	heading "Cloning wla-dx..."
	git clone https://github.com/vhelin/wla-dx "$SETUP_DIR/wla-dx"
	cd "$SETUP_DIR/wla-dx"
	git -c advice.detachedHead=false checkout v10.6

	heading "Building wla-dx..."
	rm -R build 2>/dev/null
	mkdir build && cd build && cmake .. -DCMAKE_POLICY_VERSION_MINIMUM=3.5 && cmake --build . --config Release -- wla-gb wlalink

	heading "Copying wla-dx binaries to /usr/local/bin..."
	mkdir -p /usr/local/bin 2>/dev/null
	cp "$SETUP_DIR"/wla-dx/build/binaries/* /usr/local/bin/
fi

if [[ -e "$SETUP_DIR/oracles-disasm" ]]; then
   heading "Skipping oracles-disasm git clone (folder exists already)."
else
	heading "Cloning oracles-disasm..."
	git clone https://github.com/stewmath/oracles-disasm "$SETUP_DIR/oracles-disasm"

	heading "Checking out hack-base branch..."
	cd "$SETUP_DIR/oracles-disasm" && git checkout hack-base
fi

heading "Dependencies and oracles-disasm downloaded, now you can run LynnaLab!"
