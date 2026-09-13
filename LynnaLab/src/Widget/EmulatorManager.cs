namespace LynnaLab;

using System.Threading;
using Emulation;

public class EmulatorManager : IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorManager(OraclesEmulator emulator)
    {
        this.emulator = emulator;
        this.debugger = new EmulatorDebugger(this);
        this.renderer = new EmulatorRenderer(this);

        this.debugger.Active = true;
        this.renderer.Active = true;

        this.renderer.ClosedEvent += (_, _) =>
        {
            SetPaused(true);
        };
        this.renderer.OpenedEvent += (_, _) =>
        {
            SetPaused(false);
        };

        paused = true; // Set to true to enable SetPaused(false) to do something
        SetPaused(false);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    OraclesEmulator emulator;
    EmulatorDebugger debugger;
    EmulatorRenderer renderer;
    bool paused;
    Thread? emuThread;

    // ================================================================================
    // Properties
    // ================================================================================

    public OraclesEmulator Emulator { get { return emulator; } }
    public bool Paused { get { return paused; } }

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

    public void SetPaused(bool value)
    {
        if (paused == value)
            return;

        paused = value;
        if (paused)
        {
            if (emuThread?.IsAlive ?? false)
                emuThread.Join();
            emuThread = null;
        }
        else
        {
            Debug.Assert(emuThread == null);
            emuThread = new Thread(RunEmuThreadCallback);
            emuThread.Start();
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================


    void RunEmuThreadCallback()
    {
        while (!Paused)
        {
            lock (emulator)
            {
                emulator.Run();
            }
        }
    }
}
