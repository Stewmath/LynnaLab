#version 330 core

in vec2 in_position;

out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = vec4(in_position, 0, 1);
    texCoord = in_position * 0.5 + 0.5;
    color = vec4(1, 1, 1, 1);
}
