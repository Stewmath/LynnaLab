#nullable enable
namespace LynnaLib;

using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SameBoySharp;

public class OraclesEmulator : IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public OraclesEmulator(string romPath)
    {
        gb = new SameBoy(SameBoy.Model.GB_MODEL_CGB_E);
        gb.LoadBootRomFromBuffer(Helper.ReadResourceFileBytes("LynnaLib.resource.cgb_boot_fast.bin"));
        gb.LoadRom(romPath);
        gb.DebuggerLoadSymbolFile(Path.ChangeExtension(romPath, ".sym"));

        gb.SetRgbEncodeCallback(Color.RgbToUInt);

        pixelData = Marshal.AllocHGlobal(160 * 144 * 4);

        // Fill the pixel data with black to start
        for (int i = 0; i < 160 * 144 * 4; i++)
        {
            Marshal.WriteByte(pixelData + i, 0);
        }

        gb.SetPixelsOutput(pixelData);

        Initialize();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    SameBoy gb;
    IntPtr pixelData;

    // ================================================================================
    // Properties
    // ================================================================================

    public SameBoy GB { get { return gb; } }

    public IntPtr PixelData { get { return pixelData; }}

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Dispose()
    {
        gb.Dispose();
        gb = null!;
        Marshal.FreeHGlobal(pixelData);
        pixelData = IntPtr.Zero;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Before exiting the constructor, we run to the end of the bootrom. Even though we're using
    /// the fast bios which boots to the game instantly, it has an annoying chime at the beginning.
    /// So let's execute this before sound sample callbacks have been established.
    /// </summary>
    void Initialize()
    {
        gb.SetTurboMode(true);

        while (gb.Registers.PC != 0x100)
            gb.Run();

        gb.WriteMemory(0xff12, 0); // Fix fast bios chime noise

        gb.SetTurboMode(false);
    }

    /// <summary>
    /// An alternative to using the fast BIOS. Less accurate, but works for the Oracles.
    /// </summary>
    void InitializeWithoutBios()
    {
        gb.WriteMemory(0xff4c, 0x00); // GBC hardware mode
        gb.WriteMemory(0xff50, 0x01); // Disable BIOS mapping

        // - LCD state?
        // - Audio setup
        // - Clear VRAM
        // - Clear WRAM
        // - Clear OAM

        gb.Registers.SP = 0xfffe;
        gb.Registers.A = 0x11; // GBC
        gb.Registers.BC = 0x0113;
        gb.Registers.DE = 0x00d8;
        gb.Registers.HL = 0x014d;
        gb.Registers.PC = 0x100; // Entry point
    }
}
