using System.Diagnostics;

namespace LynnaLab;

/// <summary>
/// Class containing all project-specific information.
/// Keeping this separate from the TopLevel class just in case I want to make a way to open
/// multiple projects at once.
/// </summary>
public class ProjectWorkspace
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ProjectWorkspace(Project project)
    {
        this.Project = project;

        Project.LazyInvoke = TopLevel.LazyInvoke;

        tilesetTextureCacher = new((tileset) => new TilesetTextureCacher(this, tileset));
        roomTextureCacher = new((layout) => new RoomTextureCacher(this, layout));
        mapTextureCacher = new((tup) => new MapTextureCacher(this, tup.map, tup.floor));

        // Create all world map cachers immediately because we need them available as a canvas to
        // draw room layouts (even if we're not displaying the maps themselves right away).
        for (int g=0; g<Project.NumGroups; g++)
        {
            Project.ForEachSeason(g, (s) => mapTextureCacher.GetOrCreate((Project.GetWorldMap(g, s), 0)));
        }

        this.Brush = new Brush<int>(0);

        linkTexture = TopLevel.TextureFromBitmapTracked(project.LinkBitmap);

        roomEditor = new RoomEditor(this);
        dungeonEditor = new DungeonEditor(this, "Dungeon Editor");
        tilesetEditor = new TilesetEditor(this, "Tileset Editor");
        tilesetCloner = new TilesetCloner(this, "Tileset Cloner");
        buildDialog = new BuildDialog(this, "Build");
        documentationDialog = new DocumentationDialog(this, "Documentation Dialog");
        scratchpad = new ScratchPad(this, "Scratchpad", roomEditor.TilesetViewer, Brush);
        undoDialog = new UndoDialog(this, "Undo History");

        roomEditor.SetInterpolation(bicubicScaling ? Interpolation.Bicubic : Interpolation.Nearest);
        roomEditor.SetScrollToZoom(scrollToZoom);

        frames.AddRange(new Frame[] {
                roomEditor,
                dungeonEditor,
                tilesetEditor,
                tilesetCloner,
                scratchpad,
                buildDialog,
                documentationDialog,
                undoDialog,
            });
        frames.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

        // Default active windows
        roomEditor.Active = true;

        WatchForRoomChanges();
    }

    // ================================================================================
    // Constants
    // ================================================================================

    readonly Vector2 TOOLBAR_BUTTON_SIZE = new Vector2(30.0f, 30.0f);

    // ================================================================================
    // Variables
    // ================================================================================

    RoomEditor roomEditor;
    DungeonEditor dungeonEditor;
    TilesetEditor tilesetEditor;
    TilesetCloner tilesetCloner;
    ScratchPad scratchpad;
    BuildDialog buildDialog;
    DocumentationDialog documentationDialog;
    UndoDialog undoDialog;

    List<Frame> frames = new List<Frame>();
    bool showDebugWindow;
    bool showImGuiDemoWindow;
    bool lightMode, scrollToZoom = true, darkenUsedDungeonRooms = true, bicubicScaling = true;
    bool autoAdjustGroupNumber = true;

    TextureBase linkTexture;

    Cacher<Tileset, TilesetTextureCacher> tilesetTextureCacher;
    Cacher<RoomLayout, RoomTextureCacher> roomTextureCacher;
    Cacher<(Map map, int floor), MapTextureCacher> mapTextureCacher;

    Process emulatorProcess;

    // ================================================================================
    // Properties
    // ================================================================================
    public Project Project { get; private set; }
    public QuickstartData QuickstartData { get; set; } = new QuickstartData();
    public Brush<int> Brush { get; private set; }

    public bool ShowBrushPreview { get; private set; } = true;
    public bool ViewObjects { get; private set; }
    public bool ViewWarps { get; private set; }

    // For copy/pasting. This may or may not exist in the game (it may have been deleted), but it
    // should be copiable regardless.
    public ObjectDefinition CopiedObject { get; set; }
    public Color? CopiedColor { get; set; }

    // Togglable settings that affect other modules (really just minimaps right now)
    public bool DarkenUsedDungeonRooms { get { return darkenUsedDungeonRooms; } }
    public bool AutoAdjustGroupNumber { get { return autoAdjustGroupNumber; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render(float deltaTime)
    {
        if (Project == null)
            return;

        float menuBarHeight = 0.0f;
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("Project"))
            {
                if (ImGui.MenuItem("Open"))
                {
                    TopLevel.CloseProjectModal(() => TopLevel.OpenProjectModal());
                }
                if (ImGui.MenuItem("Save"))
                {
                    Project.Save();
                }
                if (ImGui.MenuItem("Close"))
                {
                    TopLevel.CloseProjectModal();
                }
                if (ImGui.MenuItem("Reload"))
                {
                    TopLevel.CloseProjectModal(() => TopLevel.OpenProject(Project.BaseDirectory, Project.GameString));
                }
                if (ImGui.MenuItem("Switch Game"))
                {
                    TopLevel.CloseProjectModal(() =>
                    {
                        string gameString = Project.Game == Game.Seasons ? "ages" : "seasons";
                        TopLevel.OpenProject(Project.BaseDirectory, gameString);
                    });
                }
                if (ImGui.MenuItem("Run"))
                {
                    RunGame();
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Edit"))
            {
                Func<bool> renderUndoButton;
                if (Project.UndoState.UndoAvailable)
                    renderUndoButton = () => ImGui.Selectable("Undo: " + Project.UndoState.GetUndoDescription());
                else
                    renderUndoButton = () => ImGui.Selectable("Undo", false, ImGuiSelectableFlags.Disabled);

                Func<bool> renderRedoButton;
                if (Project.UndoState.RedoAvailable)
                    renderRedoButton = () => ImGui.Selectable("Redo: " + Project.UndoState.GetRedoDescription());
                else
                    renderRedoButton = () => ImGui.Selectable("Redo", false, ImGuiSelectableFlags.Disabled);

                if (renderUndoButton())
                {
                    Project.UndoState.Undo();
                }
                if (renderRedoButton())
                {
                    Project.UndoState.Redo();
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("View"))
            {
                ImGuiX.MenuItemCheckbox("View objects",
                                        new Accessor<bool>(() => ViewObjects),
                                        (_) => roomEditor.UpdateRoomComponents());
                ImGuiX.MenuItemCheckbox("View warps",
                                        new Accessor<bool>(() => ViewWarps),
                                        (_) => roomEditor.UpdateRoomComponents());
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Windows"))
            {
                foreach (Frame frame in frames)
                {
                    ImGuiX.MenuItemCheckbox(
                        frame.Name,
                        frame.Active,
                        (active) =>
                        {
                            frame.Active = active;
                        });
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Minimap"))
            {
                if (ImGuiX.MenuItemCheckbox("Scroll to Zoom", ref scrollToZoom))
                {
                    roomEditor.SetScrollToZoom(scrollToZoom);
                }

                ImGuiX.MenuItemCheckbox("Darken used dungeon rooms & duplicate rooms", ref darkenUsedDungeonRooms);
                ImGuiX.TooltipOnHover("Rooms which are darkened have a more \"canonical\" version somewhere else, either on the dungeon tab or in a different world index. Duplicate rooms may be missing their warp data.");

                if (ImGuiX.MenuItemCheckbox("Bicubic scaling", ref bicubicScaling))
                {
                    Interpolation interp = bicubicScaling ? Interpolation.Bicubic : Interpolation.Nearest;
                    roomEditor.SetInterpolation(interp);
                }
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Misc"))
            {
                ImGuiX.MenuItemCheckbox("Auto-Adjust World Number", ref autoAdjustGroupNumber);
                ImGuiX.TooltipOnHover("The subrosia map & dungeons have duplicates in the World tab. Check this box to auto-adjust the group number to its expected value when selecting these rooms.");

                if (ImGuiX.MenuItemCheckbox("Light Mode", ref lightMode))
                {
                    if (lightMode)
                        ImGui.StyleColorsLight();
                    else
                        ImGui.StyleColorsDark();
                }
                ImGuiX.MenuItemCheckbox(
                    "Hover preview",
                    new Accessor<bool>(() => ShowBrushPreview
                    ));

                if (ImGui.MenuItem("Choose emulator path..."))
                {
                    string cmd = BuildDialog.SelectEmulator();
                    if (cmd != null)
                        TopLevel.GlobalConfig.EmulatorCommand = cmd;
                }

                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Debug"))
            {
                ImGuiX.MenuItemCheckbox("Debug Window", ref showDebugWindow);
                ImGuiX.MenuItemCheckbox("ImGui Demo Window", ref showImGuiDemoWindow);
                #if RENDERDOC
                if (ImGui.MenuItem("RenderDoc: Grab frame"))
                    TopLevel.Backend.TriggerRenderDocCapture();
                if (ImGui.MenuItem("RenderDoc: Launch UI"))
                    TopLevel.Backend.RenderDocUI();
                #endif
                ImGui.EndMenu();
            }
            menuBarHeight = ImGui.GetWindowHeight();
            ImGui.EndMainMenuBar();
        }

        if (ImGuiX.BeginToolbar("Toolbar", menuBarHeight, 50.0f))
        {
            if (ImGui.ImageButton("Run", TopLevel.PegasusSeedTexture.GetBinding(), TOOLBAR_BUTTON_SIZE))
            {
                RunGame();
            }
            ImGuiX.TooltipOnHover("Run (F5)");

            ImGui.SameLine();
            ImGuiX.ToggleImageButton("Quickstart", linkTexture.GetBinding(), TOOLBAR_BUTTON_SIZE,
                    QuickstartData.Enabled, ToggleQuickstart);
            ImGuiX.TooltipOnHover("Toggle Quickstart (F4)");
        }
        ImGui.End();

        if (showDebugWindow)
        {
            ImGui.Begin("Debug", ref showDebugWindow);
            ImGui.Text("Frametime: " + deltaTime);
            ImGui.End();
        }

        if (showImGuiDemoWindow)
        {
            ImGui.PushFont(TopLevel.DefaultFont);
            ImGui.ShowDemoWindow(ref showImGuiDemoWindow);
            ImGui.PopFont();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.F4))
            ToggleQuickstart(!QuickstartData.Enabled);
        if (ImGui.IsKeyPressed(ImGuiKey.F5))
            RunGame();
        if (ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.Z))
            Project.UndoState.Undo();
        if (ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.ModShift | ImGuiKey.Z))
            Project.UndoState.Redo();

        // Rendering frames should be the last thing done. In particular, undo/redo operations
        // should happen before this. They may delete resources (ImGui images) that the frames have
        // requested to use already if the frames are rendered first.
        foreach (var frame in frames)
        {
            frame.RenderAsWindow();
        }

        RedrawTextures();
    }

    /// <summary>
    /// This is called when the Project should be closed.
    /// </summary>
    public void Close()
    {
        Project.Close();
        linkTexture.Dispose();
        tilesetTextureCacher.Dispose();
        roomTextureCacher.Dispose();
        mapTextureCacher.Dispose();
    }

    public TextureBase GetCachedTilesetTexture(Tileset tileset)
    {
        return tilesetTextureCacher.GetOrCreate(tileset).GetTexture();
    }

    public TextureBase GetCachedRoomTexture(RoomLayout layout)
    {
        return roomTextureCacher.GetOrCreate(layout).GetTexture();
    }

    public RgbaTexture GetCachedMapTexture((Map map, int floor) key)
    {
        return mapTextureCacher.GetOrCreate(key).GetTexture();
    }

    public void OpenTilesetCloner(RealTileset source, RealTileset dest)
    {
        tilesetCloner.SetSourceTileset(source);
        tilesetCloner.SetDestTileset(dest);
        tilesetCloner.Focus();
    }

    /// <summary>
    /// Set the current active emulator process, killing the previous process if it exists.
    /// </summary>
    public void RegisterEmulatorProcess(Process process)
    {
        if (emulatorProcess != null)
        {
            emulatorProcess.Kill(true); // Pass true to kill whole process tree
        }
        emulatorProcess = process;
    }

    public void ShowDocumentation(Documentation documentation)
    {
        documentationDialog.SetDocumentation(documentation);
        documentationDialog.Active = true;
        documentationDialog.Focus();
    }

    public void OpenTilesetEditor(RealTileset tileset)
    {
        tilesetEditor.SetTileset(tileset);
        tilesetEditor.Focus();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void ToggleQuickstart(bool enabled)
    {
        QuickstartData.group = (byte)roomEditor.Room.Group;
        QuickstartData.room = (byte)(roomEditor.Room.Index & 0xff);
        QuickstartData.season = (byte)roomEditor.Season;
        QuickstartData.x = 0x48;
        QuickstartData.y = 0x48;
        QuickstartData.Enabled = enabled;
    }

    void RunGame()
    {
        Project.Save();
        buildDialog.BeginCompile();
        buildDialog.Focus();
    }

    HashSet<RoomLayout> modifiedRoomLayouts = new();

    /// <summary>
    /// Checks for modified tileset & overworld images, and updates textures as needed.
    /// </summary>
    void RedrawTextures()
    {
        foreach (var ts in tilesetTextureCacher.Values)
            ts.UpdateFrame();

        var redrawRoom = (RoomLayout layout) =>
        {
            int roomX = layout.Room.Index % 16;
            int roomY = (layout.Room.Index % 256) / 16;

            // Redraw on overworld
            Map map = Project.GetWorldMap(layout.Group, layout.Season);
            var worldMapCache = mapTextureCacher.GetOrCreate((map, 0)); // This creates the cacher if it doesn't exist
            worldMapCache.RedrawRoom(layout);

            // Redraw in dungeons.
            // To reduce the number of CopyTexture calls, this redraws using the updated version
            // from the world map we just drew, rather than doing another tile-by-tile rendering of
            // the room. This should be fast enough even for the dungeon room "00" that would need
            // to be redrawn on many different maps each time a tile is changed.
            for (int d=0; d<Project.NumDungeons; d++)
            {
                Dungeon dungeon = Project.GetDungeon(d);
                for (int f=0; f<dungeon.NumFloors; f++)
                {
                    if (mapTextureCacher.TryGetValue((dungeon, f), out var mapCache))
                    {
                        mapCache.RedrawRoomFrom(layout, worldMapCache, roomX, roomY);
                    }
                }
            }
        };

        foreach (var layout in modifiedRoomLayouts)
        {
            redrawRoom(layout);
        }

        modifiedRoomLayouts.Clear();
    }

    /// <summary>
    /// Install event handlers on all rooms so that the appropriate rooms will be redrawn later.
    /// </summary>
    void WatchForRoomChanges()
    {
        // Install modified handlers for a particular season
        var installHandlers = (Room room, Season s) =>
        {
            RoomLayout layout = room.GetLayout(s);

            EventWrapper<Tileset> tilesetEventWrapper = new();
            tilesetEventWrapper.ReplaceEventSource(layout.Tileset);

            var modifiedHandler = () => modifiedRoomLayouts.Add(layout);

            // Tile layout modified
            layout.LayoutChangedEvent += (_, _) => modifiedHandler();

            // Tileset tile modified
            tilesetEventWrapper.Bind<int>("TileModifiedEvent", (_, _) => modifiedHandler());

            // Tileset index changed
            layout.TilesetChangedEvent += (_, _) =>
            {
                tilesetEventWrapper.ReplaceEventSource(layout.Tileset);
                modifiedHandler();
            };
        };

        // Iterate through all seasons
        for (int r=0; r<Project.NumRooms; r++)
        {
            Room room = Project.GetIndexedDataType<Room>(r);

            if (room.HasSeasons)
            {
                for (int s=0; s<4; s++)
                    installHandlers(room, (Season)s);
            }
            else
                installHandlers(room, Season.None);
        }
    }
}

/// <summary>
/// Little class to hold quickstart state
/// </summary>
public class QuickstartData
{
    // ================================================================================
    // Variables
    // ================================================================================
    public byte group, room, season, y, x;
    bool _enabled;

    // ================================================================================
    // Properties
    // ================================================================================
    public bool Enabled
    {
        get { return _enabled; }
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                enableToggledEvent?.Invoke(this, value);
            }
        }
    }

    // ================================================================================
    // Events
    // ================================================================================
    public event EventHandler<bool> enableToggledEvent;
}
