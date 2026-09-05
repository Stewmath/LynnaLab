#!/bin/sh
glslangValidator -V imgui-vertex.glsl -o imgui-vertex.spv -S vert     || exit 1
glslangValidator -V imgui-frag.glsl -o imgui-frag.spv -S frag         || exit 1
glslangValidator -V tileset-vertex.glsl -o tileset-vertex.spv -S vert || exit 1
glslangValidator -V tileset-frag.glsl -o tileset-frag.spv -S frag     || exit 1
