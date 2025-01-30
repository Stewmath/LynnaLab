#version 450 core

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 in_position;

layout (location = 0) out vec4 color;
layout (location = 1) out vec2 texCoord;

out gl_PerVertex
{
    vec4 gl_Position;
};

void main()
{
    gl_Position = vec4(in_position, 0, 1);
    texCoord = in_position * 0.5 + 0.5;
    color = vec4(1, 1, 1, 1);

    // The vertices I passed in are for the OpenGL coordinate space; need conversion in vulkan.
    gl_Position.y = -gl_Position.y;
}
