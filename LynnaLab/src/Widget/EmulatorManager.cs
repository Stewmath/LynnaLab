namespace LynnaLab;

using System.Collections.Concurrent;
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
        this.renderer = new EmulatorRenderer(this);
        if (project != null)
            this.ProjectInterfacer = new(project);

        this.renderer.Active = true;

        this.ClosedEvent += (_, _) => QueueDebuggerCommand("interrupt");

        emulator.GB.SetLogCallback(AppendDebuggerText);
        emulator.GB.SetInputCallback(SyncInputCallback);
        emulator.GB.SetAsyncInputCallback(AsyncInputCallback);

        emuThread = new Thread(RunEmuThreadCallback);
        emuThread.Start();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    const string DebuggerHelpText = """
This is the SameBoy debugger console.

Examples to get you started:

- See Link's health:
    > p [wLinkHealth]

- Watchpoint triggering when Link gains or loses health:
    > watch wLinkHealth

- Breakpoint that triggers on textboxes:
    > break showText

- Resume execution after a watchpoint or breakpoint triggers:
    > continue

- Get general help, or help on a specific command:
    > help
    > help watch

""";

    OraclesEmulator emulator;
    EmulatorRenderer renderer;
    readonly Thread emuThread;

    // For sameboy debugger console
    readonly ConcurrentQueue<string> debuggerCommands = new();
    readonly AutoResetEvent debuggerCommandReady = new(false);
    readonly CancellationTokenSource emuCancellation = new();
    readonly object debuggerTextLock = new();
    readonly Mutex emuMutex = new();
    string debuggerText = DebuggerHelpText;
    bool disposed;
    string debuggerCurrentLine = "";
    string debuggerLastLine = "";
    int debuggerJumpToBottom;

    // ImGui state
    int hoveringObject;
    int rightClickObjectAddr;
    int selectedMusic = 0;
    int selectedSfx = 0;
    int selectedTreasureID = 0;
    int selectedTreasureParameter = 0;
    int memoryIndex = 0;

    // ================================================================================
    // Properties
    // ================================================================================

    public OraclesEmulator Emulator { get { return emulator; } }
    public bool Paused { get { return Emulator.GB.DebuggerIsStopped(); } }

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
        emuMutex.WaitOne();
        try
        {
            renderer.Render();
            ImGui.SameLine();

            ImGui.BeginChild("Debugger pane", ImGuiChildFlags.Borders);

            if (ImGui.BeginTabBar("Debugger tabs"))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    RenderGeneralTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Object"))
                {
                    RenderObjectTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Treasure"))
                {
                    RenderTreasureManagementTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Global Flags"))
                {
                    RenderGlobalFlagsTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Debugger"))
                {
                    RenderDebuggerTab();
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
        finally
        {
            emuMutex.ReleaseMutex();
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;

        emuCancellation.Cancel();
        debuggerCommandReady.Set();
        emuThread.Join();

        renderer.Dispose();
        emuMutex.Dispose();
        debuggerCommandReady.Dispose();
        emuCancellation.Dispose();
    }

    public void Interrupt()
    {
        QueueDebuggerCommand("interrupt");
    }

    public void Continue()
    {
        QueueDebuggerCommand("continue");
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
        while (!emuCancellation.IsCancellationRequested)
        {
            emuMutex.WaitOne();
            try
            {
                // To improve performance, call Run() many times before relinquishing the mutex.
                // This is important since it only runs about one opcode per invocation.
                for (int i = 0; i < 1000; i++)
                {
                    emulator.Run();
                }
            }
            finally
            {
                emuMutex.ReleaseMutex();
            }
        }
    }

    void QueueDebuggerCommand(string command)
    {
        debuggerCommands.Enqueue(command);
        debuggerCommandReady.Set();

        lock (debuggerTextLock)
        {
            AppendDebuggerText('\n' + ("> " + command) + '\n');
            debuggerJumpToBottom = 5;
        }
    }

    /// <summary>
    /// Input callback while emulator is paused.
    /// </summary>
    string? SyncInputCallback()
    {
        emuMutex.ReleaseMutex();
        try
        {
            string? command;
            while (!emuCancellation.IsCancellationRequested)
            {
                if (debuggerCommands.TryDequeue(out command))
                    return command;

                debuggerCommandReady.WaitOne();
            }

            return "continue";
        }
        finally
        {
            emuMutex.WaitOne();
        }
    }

    /// <summary>
    /// Input callback while emulator is running.
    /// </summary>
    string? AsyncInputCallback()
    {
        if (debuggerCommands.TryDequeue(out string? command))
            return command;

        return null;
    }

    /// <summary>
    /// Can be called from rendering thread or GB execution thread
    /// </summary>
    void AppendDebuggerText(string text)
    {
        lock (debuggerTextLock)
        {
            debuggerText += text;
        }
    }

    void RenderDebuggerTab()
    {
        ImGui.PushFont(Top.MonoFont, Top.GlobalConfig.MenuFontSize);

        string text;
        lock (debuggerTextLock)
        {
            text = debuggerText;

            var size = new Vector2(0.0f, ImGui.GetWindowSize().Y * 0.8f);
            ImGui.BeginChild("CHILD", size, 0, ImGuiWindowFlags.AlwaysVerticalScrollbar);
            foreach (string line in text.Split('\n'))
            {
                ImGui.TextWrapped(line);
            }

            if (debuggerJumpToBottom != 0)
            {
                ImGui.SetScrollY(ImGui.GetScrollMaxY());
                debuggerJumpToBottom--;
            }

            ImGui.EndChild();

            if (debuggerJumpToBottom == 1)
            {
                ImGui.SetKeyboardFocusHere();
            }
        }

        if (ImGui.InputText("##Text input", ref debuggerCurrentLine, Annotation.MAX_TEXT_LENGTH,
            ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (debuggerCurrentLine.Trim().Length == 0)
                debuggerCurrentLine = debuggerLastLine;

            QueueDebuggerCommand(debuggerCurrentLine);
            debuggerLastLine = debuggerCurrentLine;
            debuggerCurrentLine = "";
        }

        ImGui.PopFont();
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

            ImGui.Text($"Flags Address: {flagPtr.Addr:X4}");

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

    void RenderTreasureManagementTab()
    {
        if (Project == null)
        {
            ImGui.Text("Need an active project.");
            return;
        }

        ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);

        if (ImGui.BeginTabBar("Treasure Tabs"))
        {
            if (ImGui.BeginTabItem("Give/Lose"))
            {
                ImGuiLL.ComboBoxFromConstants("Treasure",
                    Project.TreasureMapping,
                    ref selectedTreasureID,
                    withIntInput: true,
                    omitPrefix: false,
                    showHelp: false);
                ImGuiX.InputHex("Parameter (level/quantity/etc)", ref selectedTreasureParameter,
                    min: 0, max: 255);

                if (ImGui.Button("Give treasure"))
                {
                    Emulator.GiveTreasure(
                        (byte)selectedTreasureID,
                        (byte)selectedTreasureParameter);
                }
                ImGui.SameLine();
                if (ImGui.Button("Lose treasure"))
                {
                    Emulator.LoseTreasure((byte)selectedTreasureID);
                }

                ImGui.Separator();

                if (Emulator.GetTreasureParameterPtr(selectedTreasureID, out GbPointer ptr))
                {
                    ByteWidget($"{Emulator.DescribeAddress(ptr)} (current parameter)", ptr);
                }

                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Obtained Treasure Flags"))
            {
                if (ImGui.BeginChild("Treasure flags"))
                {
                    ConstantsMapping mapping = Project.TreasureMapping;

                    for (int t = 0; t < Project.NumTreasures; t++)
                    {
                        Accessor<bool> accessor = new(
                            () => Emulator.Snapshot.GetTreasureFlag(t),
                            (v) => Emulator.Snapshot.SetTreasureFlag(t, v));
                        ImGuiX.Checkbox(mapping.ByteToString((byte)t), accessor);
                    }
                }
                ImGui.EndChild();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.PopItemWidth();
    }

    void RenderGlobalFlagsTab()
    {
        if (Project == null)
        {
            ImGui.Text("Need a project loaded.");
            return;
        }

        if (ImGui.BeginChild("Global flags"))
        {
            for (int i = 0; i < Project.Eval("NUM_GLOBALFLAGS"); i++)
            {
                Accessor<bool> accessor = new(
                    () => Emulator.Snapshot.GetGlobalFlag(i),
                    (v) => Emulator.Snapshot.SetGlobalFlag(i, v));
                ImGuiX.Checkbox(Project.GlobalFlagMapping.ByteToString((byte)i), accessor);
            }
            ImGui.EndChild();
        }
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

        // Prompt to spawn an object
        void SpawnModalCommon(GbPointer ptr, EmulatorObjectClass cls, bool treasureObject)
        {
            int chosenID = 0;
            int chosenSubID = 0;
            int chosenVar03 = 0;
            int chosenX = 0x38, chosenY = 0x38;
            string name = treasureObject ? "Treasure Object" : cls.Name;

            Modal.OpenModal($"Spawn {name}", () =>
            {
                // Need a minimum width so that size doesn't fluctuate
                ImGui.BeginChild("Child", ImGuiX.Unit(550.0f, 0.0f), ImGuiChildFlags.AutoResizeY);

                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);

                if (treasureObject)
                {
                    chosenID = 0x60;

                    // Treasure ID is actually SubID, SubID is actually var03. I guess.

                    ImGuiLL.ComboBoxFromConstants("Treasure", Project.TreasureMapping, ref chosenSubID,
                        withIntInput: true,
                        omitPrefix: false,
                        showHelp: false);

                    ImGuiX.InputHex("SubID", ref chosenVar03);

                    if (chosenSubID < Project.NumTreasures)
                    {
                        var group = Project.GetIndexedDataType<TreasureGroup>(chosenSubID);
                        ImGui.SameLine();
                        string name = group.GetTreasureObject(chosenVar03)?.Name ?? "Non-existent";
                        ImGui.Text($"({name})");
                    }
                }
                else
                {
                    ImGuiLL.ComboBoxFromConstants("ID", cls.ConstantsMapping, ref chosenID,
                        withIntInput: true,
                        omitPrefix: false,
                        showHelp: false);
                    ImGuiX.InputHex("SubID", ref chosenSubID);
                    ImGuiX.InputHex("Var03", ref chosenVar03);
                }

                ImGuiX.InputHex("X", ref chosenX);
                ImGuiX.InputHex("Y", ref chosenY);
                ImGui.PopItemWidth();

                bool retval = false;
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
                        Emulator.Snapshot.WriteByte(ptr + 3, (byte)chosenVar03);
                        Emulator.Snapshot.WriteByte(ptr + 0xb, (byte)chosenY);
                        Emulator.Snapshot.WriteByte(ptr + 0xd, (byte)chosenX);
                    }
                    retval = true;
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    retval = true;
                }

                ImGui.EndChild();
                return retval;
            });
        }
        void SpawnModal(GbPointer ptr, EmulatorObjectClass cls)
        {
            SpawnModalCommon(ptr, cls, false);
        }

        // Like above, but for treasure objects specifically
        void SpawnTreasureObjectModal(GbPointer ptr)
        {
            SpawnModalCommon(ptr, ProjectInterfacer.InteractionObjectClass, true);
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
                GbPointer ptr = new((ushort)rightClickObjectAddr, 1);
                if (ImGui.Selectable($"Spawn {clsOverride.Name}"))
                {
                    SpawnModal(ptr, clsOverride);
                }
                if (cls.Offset == 0x40 && ImGui.Selectable("Spawn Treasure Object"))
                {
                    SpawnTreasureObjectModal(ptr);
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
        ByteWidget(label, pointer);
    }

    void ByteWidget(string label, GbPointer pointer)
    {
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
