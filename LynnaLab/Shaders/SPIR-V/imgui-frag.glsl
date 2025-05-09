#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
#extension GL_EXT_samplerless_texture_functions : enable

layout(set = 0, binding = 1) uniform sampler MainSampler1;

layout(set = 1, binding = 0) uniform texture2D Texture;
layout(set = 1, binding = 1) uniform sampler MainSampler;

layout(set = 1, binding = 2) uniform FragGlobalsStruct
{
    int interpolationMode;
    float alpha;
};

layout (location = 0) in vec4 color;
layout (location = 1) in vec2 texCoord;

layout (location = 0) out vec4 outputColor;

void main()
{
    vec4 newColor = color * texture(sampler2D(Texture, MainSampler), texCoord);
    outputColor = vec4(newColor.rgb, alpha * newColor.a);
}
