#nullable disable
namespace LynnaLab;
using Emulation;

public class EmulatorManager : IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorManager(OraclesEmulator emulator)
    {
        this.emulator = emulator;
        this.debugger = new EmulatorDebugger(emulator);
        this.renderer = new EmulatorRenderer(emulator);

        this.debugger.Active = true;
        this.renderer.Active = true;

        renderer.ClosedEvent += (_, _) =>
        {
            // Debugger controls execution flow
            Console.WriteLine("Pause");
            debugger.Pause();
        };
        renderer.OpenedEvent += (_, _) =>
        {
            debugger.Unpause();
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================
    OraclesEmulator emulator;
    EmulatorDebugger debugger;
    EmulatorRenderer renderer;

    // ================================================================================
    // Properties
    // ================================================================================

    // ================================================================================
    // Public methods
    // ================================================================================

    public void RenderAll()
    {
        debugger.RenderAsWindow("Debugger");
        renderer.RenderAsWindow("Renderer");
    }

    public void Dispose()
    {
        debugger.Dispose();
        renderer.Dispose();
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
