#nullable enable

using System.Runtime.InteropServices;

namespace LynnaLab;

/// <summary>
/// Helper class to render an emulator instance in ImGui
/// </summary>
public class EmulatorRenderer : Frame, IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public EmulatorRenderer(OraclesEmulator emu, int scale = 1) : base("Emulator")
    {
        this.emu = emu;
        this.scale = scale;

        screenTexture = Top.Backend.CreateTexture(160, 144);
        screenTexture.CopyPixels(emu.PixelData, 160, 144, 4);

        // On vblank, update the texture
        emu.GB.SetVBlankCallback((type) =>
        {
            screenTexture.CopyPixels(emu.PixelData, 160, 144, 4);
        });

        WindowFlags |= ImGuiWindowFlags.NoResize;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    OraclesEmulator emu;
    RgbaTexture screenTexture;

    int scale;

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGuiX.DrawImage(screenTexture, scale);
    }

    public void Dispose()
    {
        emu.GB.SetVBlankCallback(null);
        screenTexture.Dispose();
    }
}
