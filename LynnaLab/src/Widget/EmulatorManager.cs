namespace LynnaLab;

using System.Threading;
using Emulation;

public class EmulatorManager : Frame, IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorManager(OraclesEmulator emulator) : base("Emulator")
    {
        this.emulator = emulator;
        this.debugger = new EmulatorDebugger(this);
        this.renderer = new EmulatorRenderer(this);

        this.debugger.Active = true;
        this.renderer.Active = true;

        this.ClosedEvent += (_, _) =>
        {
            SetPaused(true);
        };
        this.OpenedEvent += (_, _) =>
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

    public override void Render()
    {
        renderer.Render();
        ImGui.SameLine();

        ImGui.BeginChild("Debugger pane", ImGuiChildFlags.Borders);

        if (ImGui.BeginTabBar("Debugger tabs"))
        {
            if (ImGui.BeginTabItem("Object"))
            {
                RenderObjectTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("General"))
            {
                RenderGeneralTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Debugger"))
            {
                debugger.Render();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.EndChild();
    }

    public void Dispose()
    {
        SetPaused(true);
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

    void RenderGeneralTab()
    {
        ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
        ImGui.SeparatorText("General");
        ByteWidget("wLinkHealth");
        ByteWidget("wNumEnemies");
        ByteWidget("wMapleKillCounter");
        ByteWidget("wTotalSignsDestroyed");
        WordWidget("wTotalEnemiesKilled");
        WordWidget("wTotalRupeesCollected");
        ImGui.SeparatorText("Room stuff");
        ImGui.SeparatorText("Dungeon stuff");
        ByteWidget("wSwitchState");
        ByteWidget("wSpinnerState");
        ImGui.PopItemWidth();
    }

    void RenderObjectTab()
    {
        ImGui.SeparatorText("Object Memory");
        ImGuiX.HexEditor("Hex", emulator.Wram.Slice(0x1000, 64));
    }

    // TODO: Write at vblank only

    void ByteWidget(string label)
    {
        GbPointer pointer = emulator.Evaluate(label);
        ImGuiX.InputHex(label, ref emulator.Wram[pointer.Addr & 0x1fff]);
    }

    void WordWidget(string label)
    {
        GbPointer ptr = emulator.Evaluate(label);
        int val = emulator.State.ReadWord(ptr);
        if (ImGuiX.InputHex(label, ref val))
        {
            emulator.WriteWordSync(ptr, (ushort)val);
        }
    }
}
