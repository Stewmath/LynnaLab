#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable
#extension GL_EXT_samplerless_texture_functions : enable // For textureSize

layout(set = 1, binding = 0) uniform utexture2D TilesetGfx; // R16: 1 x (8 * 16 * 16)
layout(set = 1, binding = 1) uniform utexture2D TilesetMap; // R8: 32 x 32 (subtile indices)
layout(set = 1, binding = 2) uniform utexture2D TilesetFlags; // R8: 32 x 32 (subtile flags)
layout(set = 1, binding = 3) uniform texture2D TilesetPalette; // RGBA: (8 * 4) x 1
layout(set = 1, binding = 4) uniform sampler PointSampler;

layout (location = 0) in vec4 color;
layout (location = 1) in vec2 texCoord;

layout (location = 0) out vec4 outputColor;

void main()
{
    vec4 newColor;

    uint canvasX = uint(texCoord.x * 256);
    uint canvasY = uint(texCoord.y * 256);

    //uint metaTileIndex = (canvasY / 16) * 16 + (canvasX / 16);

    uint subTileIndex = uint(texture(usampler2D(TilesetMap, PointSampler), texCoord).x) ^ 0x80;
    uint subTileFlags = uint(texture(usampler2D(TilesetFlags, PointSampler), texCoord).x);

    uint gfxX = canvasX % 8u;
    uint gfxY;

    if ((subTileFlags & 0x40u) != 0u) // Flip Y
        gfxY = subTileIndex * 8u + (7u - (canvasY % 8u));
    else
        gfxY = subTileIndex * 8u + (canvasY % 8u);

    uint palette = subTileFlags & 7u;

    // Decode a tile from the gameboy's native format.
    // TilesetGfx is a 1xY texture where each 16-bit "pixel" is actually 2 bytes representing a row
    // of 8 pixels (exactly as it is stored in the gameboy's vram).
    uint line = uint(texture(usampler2D(TilesetGfx, PointSampler), vec2(0, gfxY / float(8 * 16 * 16))).x); // 16-bit int containing the line pixels

    // The x-position in the line we're to draw
    uint x;
    if ((subTileFlags & 0x20u) != 0u) // Flip X
        x = gfxX;
    else
        x = 7u - gfxX;

    // Get the 2-bit color index (0-3)
    line = line >> x;
    uint colorIndex = (line & 1u) | ((line >> 7u) & 2u);

    colorIndex = palette * 4u + colorIndex;

    // Now get the color associated with the color index
    newColor = texture(sampler2D(TilesetPalette, PointSampler), vec2(colorIndex, 0) / textureSize(TilesetPalette, 0).x);

    outputColor = newColor * vec4(color.rgb, color.a);
}
