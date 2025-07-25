using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using Color = LynnaLib.Color;

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
            RegisterConnectionEvents(connection);
        }

        tilesetTextureCacher = new((tileset) => new TilesetTextureCacher(this, tileset));
        roomTextureCacher = new((layout) => new RoomTextureCacher(this, layout));
        mapTextureCacher = new((plan) => new MapTextureCacher(this, plan));

        // Create all world map cachers immediately because we need them available as a canvas to
        // draw room layouts (even if we're not displaying the maps themselves right away).
        for (int g=0; g<Project.NumGroups; g++)
        {
            Project.ForEachSeason(g, (s) => mapTextureCacher.GetOrCreate(Project.GetWorldMap(g, s)));
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
        transactionDialog = new TransactionDialog(this, "Transaction History");
        networkDialog = new NetworkDialog(this, "Networking");

        frames.AddRange(new Frame[] {
                roomEditor,
                dungeonEditor,
                tilesetEditor,
                tilesetCloner,
                scratchpad,
                buildDialog,
                documentationDialog,
                transactionDialog,
                networkDialog,
            });
        frames.Sort((f1, f2) => f1.Name.CompareTo(f2.Name));

        // Default active windows
        roomEditor.Active = true;

        WatchForRoomChanges();

        roomEditor.CursorPositionChangedEvent += (cursor) =>
        {
            // Let this run asynchronously. We don't want exceptions to bring down the whole program
            // if the connection goes down.
            Task t = ForEachConnection(async (conn) =>
            {
                await conn.UpdateCursorPosition(cursor);
            });
        };
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
    TransactionDialog transactionDialog;
    NetworkDialog networkDialog;

    List<Frame> frames = new List<Frame>();
    bool showImGuiDemoWindow;

    TextureBase linkTexture;

    Cacher<Tileset, TilesetTextureCacher> tilesetTextureCacher;
    Cacher<RoomLayout, RoomTextureCacher> roomTextureCacher;
    Cacher<FloorPlan, MapTextureCacher> mapTextureCacher;

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
    public bool ViewAnnotations { get; private set; } = true;

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
    public bool IsNetworkActive { get { return IsServerRunning || IsClientRunning; } }

    public Dictionary<int, RemoteState> RemoteStates { get; } = new();

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
            ImGuiX.MenuItemCheckbox("View annotations",
                                    new Accessor<bool>(() => ViewAnnotations),
                                    (_) => roomEditor.UpdateRoomComponents());
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Insert"))
        {
            if (ImGui.MenuItem("Annotation"))
            {
                ViewAnnotations = true;
                Project.AddAnnotation(roomEditor.Room.Index);
            }
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
        if (ImGui.BeginMenu("Misc"))
        {
            if (ImGui.MenuItem("Take Map Screenshot"))
            {
                CaptureMapScreenshot();
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

    public RgbaTexture GetCachedMapTexture(FloorPlan plan)
    {
        return GetMapTextureCacher(plan).GetTexture();
    }

    public MapTextureCacher GetMapTextureCacher(FloorPlan plan)
    {
        return mapTextureCacher.GetOrCreate(plan);
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
            Top.DoNextFrame(() => Modal.OpenModal("New connection request", () =>
            {
                // Checking conn.Closed doesn't actually work because we're not listening for packets
                // right now. Even if the client closes the connection we won't notice until later.
                if (conn.Closed)
                    return true;

                ImGui.Text($"New connection request from: {conn.RemoteEndPoint}");
                if (ImGui.Button("Accept"))
                {
                    Top.DoNextFrame(() => Modal.DisplayInfoMessage($"Client connected: {conn.RemoteEndPoint}."));
                    server.AcceptConnection(conn);
                    RegisterConnectionEvents(conn);
                    return true;
                }

                ImGui.SameLine();
                if (ImGui.Button("Reject"))
                {
                    server.RejectConnection(conn);
                    return true;
                }

                return false;
            }));
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

    /// <summary>
    /// Open a PNG file in an external program.
    /// </summary>
    public void OpenPNG(string pngFile)
    {
        IStream stream = Project.GetGfxStream(pngFile);

        if (!(stream is PngGfxStream pngStream))
        {
            Modal.DisplayErrorMessage("GFX file is not a PNG: " + pngFile);
            return;
        }

        var guid = Guid.NewGuid().ToString();
        string fullPath = Path.Combine(Path.GetTempPath(), guid.ToString()) + ".png";
        pngStream.SaveTo(fullPath);

        var onFileModified = () =>
        {
            // Handles image format exceptions only, not file read errors
            try
            {
                pngStream.LoadFromPngFile(fullPath);
            }
            catch (InvalidImageException e)
            {
                Modal.DisplayErrorMessage("Couldn't load PNG: Invalid colors in image.", e);
            }
        };

        FileSystemWatcher watcher = Helper.InitializeFileWatcher(fullPath, onFileModified);

        try
        {
            Process p = System.Diagnostics.Process.Start(Top.GlobalConfig.EditPngProgram, fullPath);
            p.EnableRaisingEvents = true;

            p.Exited += (_, _) => Top.DoNextFrame(() =>
            {
                if (p.ExitCode != 0)
                {
                    Modal.DisplayErrorMessage($"PNG editor program exited with code {p.ExitCode} (error occurred?)");
                }

                watcher.Dispose();
                onFileModified(); // Just in case something went weird with the watcher

                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception e)
                {
                    Modal.DisplayErrorMessage($"Error deleting temporary file {fullPath}.", e);
                }
            });
        }
        catch (Exception e)
        {
            Modal.DisplayErrorMessage("Couldn't open PNG file. Try setting your PNG editor program in LynnaLab settings.", e);
            return;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void ToggleQuickstart(bool enabled)
    {
        QuickstartData.group = (byte)roomEditor.Room.Group;
        QuickstartData.room = (byte)(roomEditor.Room.Index & 0xff);
        QuickstartData.season = roomEditor.SelectedSeason == Season.None ? (byte)0 : (byte)roomEditor.SelectedSeason;
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
        foreach (var m in mapTextureCacher.Values)
            m.UpdateFrame();

        var redrawRoom = (RoomLayout layout) =>
        {
            int roomX = layout.Room.Index % 16;
            int roomY = (layout.Room.Index % 256) / 16;

            // Redraw on overworld
            FloorPlan map = Project.GetWorldMap(layout.Group, layout.Season);
            var worldMapCache = mapTextureCacher.GetOrCreate(map); // This creates the cacher if it doesn't exist
            worldMapCache.RedrawRoom(layout);

            // Redraw in dungeons.
            // To reduce the number of CopyTexture calls, this redraws using the updated version
            // from the world map we just drew, rather than doing another tile-by-tile rendering of
            // the room. This should be fast enough even for the dungeon room "00" that would need
            // to be redrawn on many different maps each time a tile is changed.
            for (int d=0; d<Project.NumDungeons; d++)
            {
                Dungeon dungeon = Project.GetDungeon(d);
                foreach (Dungeon.Floor floor in dungeon.FloorPlans)
                {
                    if (mapTextureCacher.TryGetValue(floor, out var mapCache))
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

    // TODO: Unsubscribe on closure
    void RegisterConnectionEvents(ConnectionController connection)
    {
        if (connection.Role == NetworkRole.Client)
        {
            connection.TransactionsRejectedEvent += OnTransactionsRejected;
        }

        connection.ConnectionClosedEvent += OnConnectionClosed;

        if (connection.Closed)
            throw new Exception("Connection closed in the middle of loading");

        connection.RemoteStateChangedEvent += (id, remoteState) =>
        {
            if (remoteState == null)
                RemoteStates.Remove(id);
            else
                RemoteStates[id] = remoteState;
        };
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
    void OnConnectionClosed(ConnectionController controller, Exception exception)
    {
        controller.ConnectionClosedEvent -= OnConnectionClosed;

        if (IsClientRunning)
        {
            Top.DoNextFrame(() =>
            {
                Modal.DisplayErrorMessage("The connection to the server was closed.", exception);

                controller.TransactionsRejectedEvent -= OnTransactionsRejected;

                NetworkConnection = new None();
                Top.CloseProject();
            });
        }
        else if (IsServerRunning)
        {
            // No need to display a modal as the server handles it in "ClientDisconnectedEvent".
            RemoteStates.Remove(controller.RemoteID);
        }
        else
        {
            throw new Exception("OnConnectionClosed: No connection is active?");
        }
    }

    async Task ForEachConnection(Func<ConnectionController, Task> action)
    {
        await NetworkConnection.Match(
            (none) => Task.CompletedTask,
            (server) => server.ForEachConnection(action),
            (client) => action(client));
    }

    void CaptureMapScreenshot()
    {
        var floorPlan = roomEditor.ActiveFloorPlan;

        var onImageCaptured = (Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image) =>
        {
            try
            {
                string dir = "Screenshots";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string filename;
                if (floorPlan is WorldMap w)
                {
                    filename = $"World-{w.MainGroup}";
                    if (w.Season != Season.None)
                        filename += $"-{w.Season}";
                }
                else if (floorPlan is Dungeon.Floor f)
                {
                    filename = $"Dungeon-{f.Dungeon.Index:x2}-F{f.GetFloorIndex()}";
                }
                else
                    throw new Exception();

                string finalFilename;
                int index = 0;
                while (true)
                {
                    finalFilename = $"{dir}/{filename}-{index}.png";
                    if (!File.Exists(finalFilename))
                        break;
                    index++;
                }
                image.SaveAsPng(finalFilename);

                Top.DoNextFrame(
                    () => Modal.DisplayInfoMessage($"Saved screenshot to {Path.GetFullPath(finalFilename)}."));
            }
            catch (Exception e)
            {
                Top.DoNextFrame(
                    () => Modal.DisplayErrorMessage("Error creating screenshot (filesystem access denied?).", e));
            }
        };

        GetMapTextureCacher(floorPlan).CaptureImage(onImageCaptured);
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
