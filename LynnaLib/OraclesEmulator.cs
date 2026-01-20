#nullable enable
namespace LynnaLib;

using System.IO;
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
        gb.LoadBootRom("/usr/share/games/sameboy/cgb_boot.bin");
        gb.LoadRom(romPath);
        gb.DebuggerLoadSymbolFile(Path.ChangeExtension(romPath, ".sym"));
        gb.SetRenderingDisabled(true);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    SameBoy gb;

    // ================================================================================
    // Properties
    // ================================================================================

    public SameBoy GB { get { return gb; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Run until the boot ROM finishes. Assumes nothing has executed yet.
    /// </summary>
    public void RunBootRom()
    {
        if (gb.Registers.PC != 0)
            throw new EmulatorException("RunBootRom: Called in a non-initial state");

        gb.SetTurboMode(true);

        while (true)
        {
            gb.Run();
            if (gb.Registers.PC == 0x100)
                break;
        }

        gb.SetTurboMode(false);
    }

    public void Dispose()
    {
        gb.Dispose();
    }
}
