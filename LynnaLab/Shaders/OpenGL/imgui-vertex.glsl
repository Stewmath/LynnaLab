#version 330 core

uniform ProjectionMatrixBuffer
{
    mat4 projection_matrix;
};

// Determines the window of the source texture to read from.
// Normally, topLeft=(0, 0), bottomRight=(1, 1) to read the whole texture.
uniform SourceViewportBuffer
{
    vec2 topLeft;
    vec2 bottomRight;
};

in vec2 in_position;
in vec2 in_texCoord;
in vec4 in_color;

out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;

    texCoord = in_texCoord * (bottomRight - topLeft) + topLeft;
}
