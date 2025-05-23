#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 in_position;
layout (location = 1) in vec2 in_texCoord;
layout (location = 2) in vec4 in_color;

layout (binding = 0) uniform ProjectionMatrixBuffer
{
    mat4 projection_matrix;
};

// Determines the window of the source texture to read from.
// Normally, topLeft=(0, 0), bottomRight=(1, 1) to read the whole texture.
layout (set = 1, binding = 5) uniform SourceViewportBuffer
{
    vec2 topLeft;
    vec2 bottomRight;
};

layout (location = 0) out vec4 color;
layout (location = 1) out vec2 texCoord;

out gl_PerVertex
{
    vec4 gl_Position;
};

void main() 
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;

    texCoord = in_texCoord * (bottomRight - topLeft) + topLeft;
}
