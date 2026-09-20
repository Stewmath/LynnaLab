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
        if (project != null)
            this.ProjectInterfacer = new(project);

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
    bool paused, nextPausedState;
    Thread? emuThread;

    // ImGui state
    int hoveringObject;
    int rightClickObjectAddr;
    int selectedMusic = 0;
    int selectedSfx = 0;
    int memoryIndex = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    public OraclesEmulator Emulator { get { return emulator; } }
    public bool Paused { get { return paused; } }

    /// <summary>
    /// SelectedObject points to some area of object memory. Can be a blank slot.
    /// </summary>
    public EmulatorObject? SelectedObject { get; private set; }

    Project? Project { get { return ProjectInterfacer?.Project; } }
    EmulatorProjectInterfacer? ProjectInterfacer { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        // Reduce surprises by disallowing the emulator from running while we're rendering it and
        // the debug windows. There are no guarantees about where in the execution it may have
        // paused, though.
        lock (emulator)
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
                if (ImGui.BeginTabItem("Memory"))
                {
                    RenderMemoryTab();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            ImGui.EndChild();

            emulator.Snapshot.FlushWrites();
        }

        UpdatePauseState();
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

        nextPausedState = value;

        // To prevent a deadlock, we can't depend on the thread exiting right now if we're holding
        // the lock. Call UpdatePauseState() later.
        if (!Monitor.IsEntered(emulator))
            UpdatePauseState();
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
            SelectedObject = new(Emulator.Snapshot, (ushort)address);
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

    /// <summary>
    /// Cannot call this while emulator is in a lock (need that thread to exit).
    /// </summary>
    void UpdatePauseState()
    {
        // Must do this outside of the lock
        if (nextPausedState != paused)
        {
            paused = nextPausedState;
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

        ImGui.SeparatorText("Audio");
        if (ProjectInterfacer != null)
        {
            if (ImGui.Button("Play Music"))
            {
                Emulator.PlaySound(selectedMusic);
            }
            ImGui.SameLine();
            ImGuiLL.ComboBoxFromConstants("Play music",
                ProjectInterfacer.MusicMapping,
                ref selectedMusic,
                withIntInput: false,
                omitPrefix: false,
                showHelp: false);

            if (ImGui.Button("Play Sound Effect"))
            {
                Emulator.PlaySound(selectedSfx);
            }
            ImGui.SameLine();
            ImGuiLL.ComboBoxFromConstants("Play sound effect",
                ProjectInterfacer.SfxMapping,
                ref selectedSfx,
                withIntInput: false,
                omitPrefix: false,
                showHelp: false);
        }

        int room = Emulator.Snapshot.RoomIndex;
        ImGui.SeparatorText($"Room {room:X3}");

        if (room < 0 || room >= 0x800)
            ImGui.Text($"Invalid room index {room:X3}?.");
        else
        {
            GbPointer flagPtr = Emulator.GetRoomFlagPtr(room);

            Accessor<bool> FlagAccessor(byte bitmask)
            {
                return new(() => (Emulator.Snapshot.ReadByte(flagPtr) & bitmask) != 0,
                    (v) =>
                    {
                        if (v)
                            Emulator.Snapshot.OrByte(flagPtr, bitmask);
                        else
                            Emulator.Snapshot.AndByte(flagPtr, (byte)(0xff ^ bitmask));
                    });
            }

            ImGui.BeginTable("Room flags", 2);
            ImGui.TableNextColumn();
            ImGuiX.Checkbox("ROOMFLAG_01", FlagAccessor(0x01));
            ImGuiX.Checkbox("ROOMFLAG_02", FlagAccessor(0x02));
            ImGuiX.Checkbox("ROOMFLAG_04", FlagAccessor(0x04));
            ImGuiX.Checkbox("ROOMFLAG_08", FlagAccessor(0x08));
            ImGui.TableNextColumn();
            ImGuiX.Checkbox("ROOMFLAG_VISITED", FlagAccessor(0x10));
            ImGuiX.Checkbox("ROOMFLAG_ITEM", FlagAccessor(0x20));
            ImGuiX.Checkbox("ROOMFLAG_40", FlagAccessor(0x40));
            ImGuiX.Checkbox("ROOMFLAG_80", FlagAccessor(0x80));
            ImGui.EndTable();
        }

        ImGui.SeparatorText("Dungeon stuff");
        ByteWidget("wSwitchState");
        ByteWidget("wSpinnerState");
        ImGui.PopItemWidth();
    }

    void RenderObjectTab()
    {
        int nextHoveringObject = 0;

        if (Project == null || ProjectInterfacer == null)
        {
            ImGui.Text("Must have a project loaded.");
            goto end;
        }

        float childSize = ImGui.GetContentRegionAvail().X / 4.0f;

        void SpawnModal(GbPointer ptr, EmulatorObjectClass cls)
        {
            int chosenID = 0;
            int chosenSubID = 0;
            int chosenX = 0x38, chosenY = 0x38;
            Modal.OpenModal($"Spawn {cls.Name}", () =>
            {
                ImGuiLL.ComboBoxFromConstants("ID", cls.ConstantsMapping, ref chosenID,
                    withIntInput: true,
                    omitPrefix: false,
                    showHelp: false);

                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
                ImGuiX.InputHex("SubID", ref chosenSubID);
                ImGuiX.InputHex("X", ref chosenX);
                ImGuiX.InputHex("Y", ref chosenY);
                ImGui.PopItemWidth();

                if (ImGui.Button("OK"))
                {
                    // Best-effort check for whether the slot is in use already. But race conditions
                    // are possible.
                    if (Emulator.Snapshot.ReadByte(ptr) != 0)
                    {
                        Modal.DisplayErrorMessage($"Couldn't spawn object (slot {ptr.Addr:X4} already in use?)");
                    }
                    else
                    {
                        Emulator.Snapshot.WriteByte(ptr, 1);
                        Emulator.Snapshot.WriteByte(ptr + 1, (byte)chosenID);
                        Emulator.Snapshot.WriteByte(ptr + 2, (byte)chosenSubID);
                        Emulator.Snapshot.WriteByte(ptr + 0xb, (byte)chosenY);
                        Emulator.Snapshot.WriteByte(ptr + 0xd, (byte)chosenX);
                    }
                    return true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    return true;
                }

                return false;
            });
        }

        void RenderSection(EmulatorObjectClass cls)
        {
            string name = cls.Plural;
            if (cls.Offset == 0)
                name = "Special/Items";
            ImGui.BeginChild(name, new(childSize, 0.0f), ImGuiChildFlags.AutoResizeY);
            ImGui.SeparatorText(name);
            for (int addr = 0xd000 + cls.Offset; addr < 0xe000; addr += 0x100)
            {
                EmulatorObjectClass clsOverride = ProjectInterfacer.GetObjectClass(addr);
                EmulatorObject obj = new(Emulator.Snapshot, (ushort)addr);
                string text;
                if (obj.Enabled == 0)
                {
                    text = "----";
                }
                else
                {
                    text = clsOverride.GetName(obj.Memory[1], withPrefix: false);
                }

                if (hoveringObject == addr)
                    ImGui.TextColored(Color.LightBlue, text);
                else
                    ImGui.Text(text);

                if (ImGui.IsItemHovered())
                {
                    nextHoveringObject = addr;
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        SetSelectedObject(obj.Address);
                    }
                    else if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        ImGui.OpenPopup("EmptyRightClickMenu");
                        rightClickObjectAddr = addr;
                    }
                }
            }

            if (ImGui.BeginPopup("EmptyRightClickMenu"))
            {
                EmulatorObjectClass clsOverride = ProjectInterfacer.GetObjectClass(rightClickObjectAddr);
                if (ImGui.Selectable($"Spawn {clsOverride.Name}"))
                {
                    SpawnModal(new((ushort)rightClickObjectAddr, 1), clsOverride);
                }
                ImGui.EndPopup();
            }
            ImGui.EndChild();
        }

        ImGui.PushFont(Top.MonoFont, 0.0f);

        if (SelectedObject == null)
        {
            RenderSection(ProjectInterfacer.SpecialObjectClass); // Also does items
            ImGui.SameLine();
            RenderSection(ProjectInterfacer.InteractionObjectClass);
            ImGui.SameLine();
            RenderSection(ProjectInterfacer.EnemyObjectClass);
            ImGui.SameLine();
            RenderSection(ProjectInterfacer.PartObjectClass);
        }
        else if (ImGui.Button("Back"))
        {
            SetSelectedObject(null);
        }
        else
        {
            string name = ProjectInterfacer.GetObjectClass(SelectedObject.Address).GetName(SelectedObject.ID);
            ImGui.SeparatorText("Object Info");
            ImGui.Text($"Addr:  {SelectedObject.Address:X4}");

            if (SelectedObject.Enabled == 0)
                ImGui.Text("Object is disabled.");
            else
            {
                ImGui.Text($"ID:    {SelectedObject.Memory[1]:X2} ({name})");
                ImGui.Text($"SubID: {SelectedObject.Memory[2]:X2}");

                if (ImGui.Button("Delete"))
                {
                    SelectedObject.Delete();
                }
            }

            ImGui.SeparatorText("Object Memory");
            ImGuiX.HexEditor("Hex",
                SelectedObject.ReadByte, SelectedObject.WriteByte,
                0x40, SelectedObject.Address);
        }

        ImGui.PopFont();

        end:
        hoveringObject = nextHoveringObject;
    }

    void RenderMemoryTab()
    {
        string[] options = [
            "System Bus",
            "Wram",
        ];

        ImGui.Combo("Memory Type", ref memoryIndex, options, options.Length);

        Func<int, byte> readByte;
        Action<int, byte> writeByte;
        int length;

        if (memoryIndex == 0) // System bus
        {
            var getPtr = (int i) => new GbPointer(i, OraclesEmulator.DefaultBank(i));
            readByte = (i) => Emulator.Snapshot.ReadByte(getPtr(i));
            writeByte = (i, b) => Emulator.Snapshot.WriteByte(getPtr(i), b);
            length = 0x10000;
        }
        else if (memoryIndex == 1) // WRAM
        {
            readByte = (i) => Emulator.Snapshot.Wram[i];
            writeByte = (i, b) => Emulator.Snapshot.WriteWram(i, b);
            length = 0x8000;
        }
        else
        {
            throw new Exception();
        }

        ImGui.BeginChild($"Hex {options[memoryIndex]}");
        ImGuiX.HexEditor("Memory", readByte, writeByte, length);
        ImGui.EndChild();
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
        int val = emulator.Snapshot.ReadWord(ptr);
        if (ImGuiX.InputHex(label, ref val))
        {
            emulator.WriteWordSync(ptr, (ushort)val);
        }
    }
}
