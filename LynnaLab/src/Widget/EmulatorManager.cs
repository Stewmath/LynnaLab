namespace LynnaLab;

using System.Threading;
using Emulation;

public class EmulatorManager : Frame, IDisposable
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorManager(OraclesEmulator emulator, Project? project) : base("Emulator")
    {
        this.emulator = emulator;
        this.debugger = new EmulatorDebugger(this);
        this.renderer = new EmulatorRenderer(this);
        this.Project = project;

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

    // ImGui state
    int hoveringObject;

    // ================================================================================
    // Properties
    // ================================================================================

    public OraclesEmulator Emulator { get { return emulator; } }
    public bool Paused { get { return paused; } }

    public EmulatorObject? SelectedObject { get; private set; }

    Project? Project { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        if (SelectedObject?.Enabled == 0)
            SetSelectedObject(null);

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

    public void SetSelectedObject(ushort? address)
    {
        if (address == null)
        {
            if (SelectedObject == null)
                return;
            SelectedObject = null;
        }
        else
        {
            if (SelectedObject?.Address == address)
                return;
            SelectedObject = new(Emulator.State, (ushort)address);
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
        int nextHoveringObject = 0;

        if (Project == null)
        {
            ImGui.Text("Must have a project loaded.");
            goto end;
        }

        float childSize = ImGui.GetContentRegionAvail().X / 4.0f;

        void RenderSection(string name, int start)
        {
            ImGui.BeginChild(name, new(childSize, 0.0f), ImGuiChildFlags.AutoResizeY);
            ImGui.SeparatorText(name);
            for (int addr = 0xd000 + start; addr < 0xe000; addr += 0x100)
            {
                EmulatorObject obj = new(Emulator.State, (ushort)addr);
                if (obj.Enabled == 0)
                    ImGui.Text("----");
                else
                {
                    string text = obj.GetName(Project, false);
                    if (hoveringObject == addr)
                        ImGui.TextColored(Color.LightBlue, text);
                    else
                        ImGui.Text(text);

                    if (ImGui.IsItemHovered())
                    {
                        nextHoveringObject = addr;
                        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                            SetSelectedObject(obj.Address);
                    }
                }
            }
            ImGui.EndChild();
        }

        ImGui.PushFont(Top.MonoFont, 0.0f);

        if (SelectedObject == null)
        {
            RenderSection("Special/Item", 0x00);
            ImGui.SameLine();
            RenderSection("Interactions", 0x40);
            ImGui.SameLine();
            RenderSection("Enemies", 0x80);
            ImGui.SameLine();
            RenderSection("Parts", 0xc0);
        }
        else if (ImGui.Button("Back"))
        {
            SetSelectedObject(null);
        }
        else
        {
            string name = SelectedObject.GetName(Project);
            ImGui.SeparatorText("Object Info");
            ImGui.Text($"Addr:  {SelectedObject.Address:X4}");
            ImGui.Text($"ID:    {SelectedObject.Memory[1]:X2} ({name})");
            ImGui.Text($"SubID: {SelectedObject.Memory[2]:X2}");
            ImGui.SeparatorText("Object Memory");
            ImGuiX.HexEditor("Hex", SelectedObject.Memory, SelectedObject.Address);
        }

        ImGui.PopFont();

        end:
        hoveringObject = nextHoveringObject;
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
