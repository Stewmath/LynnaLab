using System.Diagnostics;
using System.Threading.Tasks;

namespace LynnaLab;

/// <summary>
/// Class containing all project-specific information.
/// Keeping this separate from the Top class just in case I want to make a way to open multiple
/// projects at once.
/// </summary>
public class ProjectWorkspace
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ProjectWorkspace(Project project, string uniqueIdentifier, ConnectionController connection = null)
    {
        this.Project = project;
        this.UniqueIdentifier = uniqueIdentifier;

        if (connection == null)
            this.NetworkConnection = new None();
        else
        {
            // This is a client connected to a server
            this.NetworkConnection = connection;

            connection.TransactionsRejectedEvent += OnTransactionsRejected;
            connection.ConnectionClosedEvent += OnConnectionClosed;

            if (connection.Closed)
                throw new Exception("Connection closed in the middle of loading");
        }

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

        linkTexture = Top.TextureFromBitmapTracked(project.LinkBitmap);

        roomEditor = new RoomEditor(this);
        dungeonEditor = new DungeonEditor(this, "Dungeon Editor");
        tilesetEditor = new TilesetEditor(this, "Tileset Editor");
        tilesetCloner = new TilesetCloner(this, "Tileset Cloner");
        buildDialog = new BuildDialog(this, "Build");
        documentationDialog = new DocumentationDialog(this, "Documentation Dialog");
        scratchpad = new ScratchPad(this, "Scratchpad", roomEditor.TilesetViewer, Brush);
        undoDialog = new UndoDialog(this, "Undo History");
        networkDialog = new NetworkDialog(this, "Networking");

        frames.AddRange(new Frame[] {
                roomEditor,
                dungeonEditor,
                tilesetEditor,
                tilesetCloner,
                scratchpad,
                buildDialog,
                documentationDialog,
                undoDialog,
                networkDialog,
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

    private static readonly log4net.ILog log = LogHelper.GetLogger();

    RoomEditor roomEditor;
    DungeonEditor dungeonEditor;
    TilesetEditor tilesetEditor;
    TilesetCloner tilesetCloner;
    ScratchPad scratchpad;
    BuildDialog buildDialog;
    DocumentationDialog documentationDialog;
    UndoDialog undoDialog;
    NetworkDialog networkDialog;

    List<Frame> frames = new List<Frame>();
    bool showImGuiDemoWindow;

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

    // For copy/pasting. This may or may not exist in the game (it may have been deleted), but it
    // should be copiable regardless.
    public ObjectDefinition CopiedObject { get; set; }
    public Color? CopiedColor { get; set; }

    public bool ViewObjects { get; private set; }
    public bool ViewWarps { get; private set; }

    // Togglable settings that affect other modules (really just minimaps right now)
    public bool DarkenDuplicateRooms { get { return Top.GlobalConfig.DarkenDuplicateRooms; } }
    public bool AutoAdjustGroupNumber { get { return Top.GlobalConfig.AutoAdjustGroupNumber; } }

    // Identifier applied to imgui windows. Useful for displaying multiple workspaces at once (ie.
    // when testing networking).
    public string UniqueIdentifier { get; }

    // Network status: Standalone, Server, or Client
    public OneOf<None, ServerController, ConnectionController> NetworkConnection { get; private set; }

    public bool IsServerRunning { get { return NetworkConnection.IsT1; } }
    public bool IsClientRunning { get { return NetworkConnection.IsT2; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void RenderMenuBar()
    {
        if (ImGui.BeginMenu("Project"))
        {
            if (ImGui.MenuItem("Open"))
            {
                Modal.CloseProjectModal(this, () => Modal.OpenProjectModal());
            }
            if (!IsClientRunning && ImGui.MenuItem("Save"))
            {
                Project.Save();
            }
            if (ImGui.MenuItem("Close"))
            {
                Modal.CloseProjectModal(this);
            }
            if (!IsClientRunning)
            {
                if (ImGui.MenuItem("Reload"))
                {
                    Modal.CloseProjectModal(this, () => Top.OpenProject(Project.BaseDirectory, Project.GameString));
                }
                if (ImGui.MenuItem("Switch Game"))
                {
                    Modal.CloseProjectModal(this, () =>
                    {
                        string gameString = Project.Game == Game.Seasons ? "ages" : "seasons";
                        Top.OpenProject(Project.BaseDirectory, gameString);
                    });
                }
                if (ImGui.MenuItem("Run"))
                {
                    RunGame();
                }
            }
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Edit"))
        {
            Func<bool> renderUndoButton;
            if (Project.TransactionManager.UndoAvailable)
                renderUndoButton = () => ImGui.Selectable("Undo: " + Project.TransactionManager.GetUndoDescription());
            else
                renderUndoButton = () => ImGui.Selectable("Undo", false, ImGuiSelectableFlags.Disabled);

            Func<bool> renderRedoButton;
            if (Project.TransactionManager.RedoAvailable)
                renderRedoButton = () => ImGui.Selectable("Redo: " + Project.TransactionManager.GetRedoDescription());
            else
                renderRedoButton = () => ImGui.Selectable("Redo", false, ImGuiSelectableFlags.Disabled);

            if (renderUndoButton())
            {
                TryUndo();
            }
            if (renderRedoButton())
            {
                TryRedo();
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
        if (ImGui.BeginMenu("Debug"))
        {
            ImGuiX.MenuItemCheckbox("ImGui Demo Window", ref showImGuiDemoWindow);
            #if RENDERDOC
            if (ImGui.MenuItem("RenderDoc: Grab frame"))
                Top.Backend.TriggerRenderDocCapture();
            if (ImGui.MenuItem("RenderDoc: Launch UI"))
                Top.Backend.RenderDocUI();
            #endif
            ImGui.EndMenu();
        }
    }

    public void RenderToolbar()
    {
        if (ImGui.ImageButton("Run", Top.PegasusSeedTexture.GetBinding(), ImGuiX.Unit(TOOLBAR_BUTTON_SIZE)))
        {
            RunGame();
        }
        ImGuiX.TooltipOnHover("Run (F5)");

        ImGui.SameLine();
        ImGuiX.ToggleImageButton("Quickstart", linkTexture.GetBinding(), ImGuiX.Unit(TOOLBAR_BUTTON_SIZE),
                QuickstartData.Enabled, ToggleQuickstart);
        ImGuiX.TooltipOnHover("Toggle Quickstart (F4)");
    }

    public void Render(float deltaTime)
    {
        if (Project == null)
            return;

        if (showImGuiDemoWindow)
        {
            ImGui.PushFont(Top.InfoFont);
            ImGui.ShowDemoWindow(ref showImGuiDemoWindow);
            ImGui.PopFont();
        }

        if (ImGui.IsKeyPressed(ImGuiKey.F4))
            ToggleQuickstart(!QuickstartData.Enabled);
        if (ImGui.IsKeyPressed(ImGuiKey.F5))
            RunGame();
        if (ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.Z))
            TryUndo();
        if (ImGui.IsKeyChordPressed(ImGuiKey.ModCtrl | ImGuiKey.ModShift | ImGuiKey.Z))
            TryRedo();

        // Rendering frames should be the last thing done. In particular, undo/redo operations
        // should happen before this. They may delete resources (ImGui images) that the frames have
        // requested to use already if the frames are rendered first.
        foreach (var frame in frames)
        {
            frame.RenderAsWindow(UniqueIdentifier);
        }

        RedrawTextures();
    }

    /// <summary>
    /// This is called when the Project should be closed.
    /// </summary>
    public void Close()
    {
        NetworkConnection.Switch(
            none => {},
            server => server.Stop(),
            client => client.Close()
        );
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

    /// <summary>
    /// Begin listening for connections for collaborative editing.
    /// </summary>
    public void BeginServer(System.Net.IPEndPoint serverAddress)
    {
        if (IsServerRunning)
            throw new Exception("BeginServer: Server already running");
        if (IsClientRunning)
            throw new Exception("BeginServer: Client already running");

        ServerController server;
        try
        {
            server = ServerController.CreateServer(Project, serverAddress, Top.DoNextFrameAsync);
            this.NetworkConnection = server;
        }
        catch (System.Net.Sockets.SocketException e)
        {
            Modal.DisplayErrorMessage("There was an error starting the server (couldn't open the socket? See log).");
            log.Error(e);
            return;
        }

        // TODO: Unsubscribe on closure
        server.ConnectionRequestedEvent += (_, conn) =>
        {
            Top.DoNextFrame(() => Modal.ConnectionRequestModal(server, conn));
        };

        server.ClientDisconnectedEvent += (_, conn, exception) =>
        {
            Top.DoNextFrame(() => Modal.DisplayErrorMessage("Client disconnected.", exception));
        };

        // New thread: Run the server until it shuts down
        Task.Run(async () =>
        {
            try
            {
                await server.RunUntilClosed();

                Top.DoNextFrame(() =>
                {
                    Modal.DisplayInfoMessage("Server has closed.");
                });
            }
            catch (Exception e)
            {
                log.Error(e);
                Top.DoNextFrame(() =>
                {
                    Modal.DisplayErrorMessage("Server encountered an error and is shutting down; see log.");
                });
            }
            finally
            {
                this.NetworkConnection = new None();
            }
        });
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

    void TryUndo()
    {
        if (!Project.TransactionManager.Undo())
            Modal.DisplayErrorMessage("Undo failed.");
    }

    void TryRedo()
    {
        if (!Project.TransactionManager.Redo())
            Modal.DisplayErrorMessage("Redo failed.");
    }

    /// <summary>
    /// For clients: This is called when the server rejects transactions.
    /// </summary>
    void OnTransactionsRejected(IEnumerable<string> list)
    {
        if (!IsClientRunning)
            throw new Exception("OnTransactionsRejected: Called on a non-client instance?");

        string message;
        if (list.Count() == 1)
            message = $"Server rejected transaction: '{list.First()}'";
        else
            message = $"Server rejected multiple transactions.";

        Modal.DisplayErrorMessage(message);
    }

    /// <summary>
    /// For clients: This is called when the connection is closed for any reason (maybe the server
    /// closed it, or maybe there was some kind of protocol error that the networking code couldn't
    /// deal with.)
    /// </summary>
    void OnConnectionClosed(Exception exception)
    {
        if (!IsClientRunning)
            throw new Exception("OnConnectionClosed: Called on a non-client instance?");

        ConnectionController controller = NetworkConnection.AsT2;

        Top.DoNextFrame(() =>
        {
            Modal.DisplayErrorMessage("The connection to the server was closed.", exception);

            controller.TransactionsRejectedEvent -= OnTransactionsRejected;
            controller.ConnectionClosedEvent -= OnConnectionClosed;

            NetworkConnection = new None();
            Top.CloseProject();
        });
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
