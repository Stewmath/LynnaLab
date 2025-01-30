#!/bin/sh
glslangValidator -V imgui-vertex.glsl -o imgui-vertex.spv -S vert
glslangValidator -V imgui-frag.glsl -o imgui-frag.spv -S frag
glslangValidator -V tileset-vertex.glsl -o tileset-vertex.spv -S vert
glslangValidator -V tileset-frag.glsl -o tileset-frag.spv -S frag
