﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Gtk;

using LynnaLab;
using LynnaLib;
using Util;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.ComponentModel;

// Doesn't extend the Gtk.Window class because the actual window is defined in Glade
// (Glade/MainWindow.ui).
public class MainWindow
{
    private static readonly log4net.ILog log = LogHelper.GetLogger();


    // Status bar message priority constants.
    // Last value has highest priority.
    enum StatusbarMessage
    {
        TileSelected,
        TileHovering,
        WrongLayoutGroup,
        RoomNotInDungeon,
        DungeonBitAndIndexMismatch,
        DungeonIndexMismatch,
        DungeonBitMismatch,
        DungeonOutsideDungeon,
        WarpDestEditMode,
    }


    // GUI stuff
    Gtk.Window mainWindow;

    Gtk.MenuBar menubar1;
    Gtk.MenuItem editMenuItem, actionMenuItem, debugMenuItem;

    Gtk.Notebook minimapNotebook, contextNotebook;
    Gtk.SpinButton worldSpinButton;
    Gtk.CheckButton viewObjectsCheckButton, viewWarpsCheckButton;
    Gtk.CheckMenuItem autoAdjustGroupCheckButton;
    Gtk.CheckButton darkenDungeonRoomsCheckbox;
    LynnaLab.HighlightingMinimap worldMinimap;
    Gtk.SpinButton dungeonSpinButton;
    Gtk.SpinButton floorSpinButton;
    LynnaLab.Minimap dungeonMinimap;
    ComboBoxFromConstants seasonComboBox;

    Gtk.Box roomVreHolder, chestAddHolder, chestEditorBox, chestVreHolder, treasureVreHolder;
    Gtk.Box nonExistentTreasureHolder, overallEditingContainer;
    Gtk.Widget treasureDataFrame;

    Gtk.Label treasureDataLabel;

    LynnaLab.SpinButtonHexadecimal roomSpinButton;
    Gtk.Button editTilesetButton;
    ValueReferenceEditor roomVre, chestVre, treasureVre;

    LynnaLab.ObjectGroupEditor objectgroupeditor1;
    LynnaLab.TilesetViewer tilesetViewer1;
    LynnaLab.RoomEditor roomeditor1;
    WarpEditor warpEditor = null;
    PriorityStatusbar statusbar1;
    BuildDialog buildDialog;

    WeakEventWrapper<ValueReference> roomTilesetModifiedEventWrapper = new WeakEventWrapper<ValueReference>();
    WeakEventWrapper<ValueReferenceGroup> tilesetModifiedEventWrapper;
    WeakEventWrapper<Chest> chestEventWrapper = new WeakEventWrapper<Chest>();

    bool changingWorld = false;

    // Variables
    uint animationTimerID = 0;
    PluginCore pluginCore;
    GlobalConfig globalConfig;
    Process emulatorProcess;
    byte[] copiedLayoutData = null;
    int copiedLayoutWidth;

    // Certain "update" events are called indirectly through this. Certain updates are delayed until
    // it is "unlocked", because we sometimes don't want updates to certain widget properties to be
    // applied immediately.
    LockableEventGroup eventGroup = new LockableEventGroup();


    // Properties

    public Gtk.Window Window
    {
        get
        {
            return mainWindow;
        }
    }

    public GlobalConfig GlobalConfig
    {
        get
        {
            return globalConfig;
        }
    }

    public Project Project { get; set; }

    public QuickstartData QuickstartData
    {
        get { return roomeditor1.QuickstartData; }
    }

    public Room ActiveRoom
    {
        get
        {
            return roomeditor1.Room;
        }
    }
    public RoomLayout ActiveRoomLayout
    {
        get
        {
            if (Project.Game == Game.Seasons && ActiveRoom?.Group == 0)
                return ActiveRoom?.GetLayout(ActiveSeason);
            else
                return ActiveRoom?.GetLayout(-1);
        }
    }
    public int ActiveSeason
    {
        get
        {
            return seasonComboBox.ActiveValue;
        }
    }
    public Map ActiveMap
    {
        get
        {
            return ActiveMinimap.Map;
        }
        set
        {
            if (ActiveMap != value && value != null)
            {
                if (value is Dungeon)
                    minimapNotebook.Page = 1;
                else
                    minimapNotebook.Page = 0;
                ActiveMinimap.SetMap(value);
                eventGroup.Invoke(OnMapChanged);
            }
        }
    }
    public int MapSelectedX
    {
        get
        {
            return ActiveMinimap.SelectedX;
        }
    }
    public int MapSelectedY
    {
        get
        {
            return ActiveMinimap.SelectedY;
        }
    }
    public int MapSelectedFloor
    {
        get
        {
            return ActiveMinimap.Floor;
        }
    }

    // Used by Tileset editor for copy/pasting colors from palettes
    public Color CopiedColor
    {
        get; set;
    }


    // Private properties

    Minimap ActiveMinimap
    {
        get
        {
            return minimapNotebook.Page == 0 ? worldMinimap : dungeonMinimap;
        }
    }

    bool WorldContextActive
    { // "World" tab
        get { return ActiveMinimap == worldMinimap; }
    }
    bool DungeonContextActive
    { // "Dungeon" tab
        get { return ActiveMinimap == dungeonMinimap; }
    }


    bool RoomContextActive
    { // "Room" tab
        get { return contextNotebook.Page == 0; }
    }
    bool ObjectContextActive
    { // "Objects" tab
        get { return contextNotebook.Page == 1; }
    }
    bool WarpContextActive
    { // "Warps" tab
        get { return contextNotebook.Page == 2; }
    }
    bool ChestContextActive
    { // "Chests" tab
        get { return contextNotebook.Page == 3; }
    }

    bool AutoAdjustGroup
    {
        get { return !changingWorld && autoAdjustGroupCheckButton.Active; }
    }

    public MainWindow(string directory = null, string game = null)
    {
        log.Debug("Beginning Program");

        globalConfig = GlobalConfig.Load();
        if (globalConfig == null)
        {
            globalConfig = new GlobalConfig();
            globalConfig.Save();
        }

        GuiSetup();

        if (directory != null)
        {
            if (!Directory.Exists(directory))
            {
                using (var d = new Gtk.MessageDialog(
                    mainWindow,
                    DialogFlags.DestroyWithParent,
                    MessageType.Warning,
                    ButtonsType.Ok,
                    $"The folder {directory} does not exist. If you're on Windows, try running windows-setup.bat first."))
                {
                    d.Run();
                }
            }
            else
                OpenProject(directory, game);
        }
    }

    void GuiSetup()
    {
        Gtk.Window.DefaultIcon = new Gdk.Pixbuf(Helper.GetResourceStream("LynnaLab.icon.ico"));

        Gtk.Builder builder = new Builder();
        builder.AddFromString(Helper.ReadResourceFile("LynnaLab.Glade.MainWindow.ui"));
        builder.Autoconnect(this);

        mainWindow = (builder.GetObject("mainWindow") as Gtk.Window);
        menubar1 = (Gtk.MenuBar)builder.GetObject("menubar1");
        editMenuItem = (Gtk.MenuItem)builder.GetObject("editMenuItem");
        actionMenuItem = (Gtk.MenuItem)builder.GetObject("actionMenuItem");
        debugMenuItem = (Gtk.MenuItem)builder.GetObject("debugMenuItem");
        minimapNotebook = (Gtk.Notebook)builder.GetObject("minimapNotebook");
        contextNotebook = (Gtk.Notebook)builder.GetObject("contextNotebook");
        worldSpinButton = (Gtk.SpinButton)builder.GetObject("worldSpinButton");
        viewObjectsCheckButton = (Gtk.CheckButton)builder.GetObject("viewObjectsCheckButton");
        viewWarpsCheckButton = (Gtk.CheckButton)builder.GetObject("viewWarpsCheckButton");
        autoAdjustGroupCheckButton = (Gtk.CheckMenuItem)builder.GetObject("autoAdjustGroupCheckButton");
        darkenDungeonRoomsCheckbox = (Gtk.CheckButton)builder.GetObject("darkenDungeonRoomsCheckbox");
        dungeonSpinButton = (Gtk.SpinButton)builder.GetObject("dungeonSpinButton");
        floorSpinButton = (Gtk.SpinButton)builder.GetObject("floorSpinButton");
        roomVreHolder = (Gtk.Box)builder.GetObject("roomVreHolder");
        chestAddHolder = (Gtk.Box)builder.GetObject("chestAddHolder");
        chestEditorBox = (Gtk.Box)builder.GetObject("chestEditorBox");
        chestVreHolder = (Gtk.Box)builder.GetObject("chestVreHolder");
        treasureVreHolder = (Gtk.Box)builder.GetObject("treasureVreHolder");
        nonExistentTreasureHolder = (Gtk.Box)builder.GetObject("nonExistentTreasureHolder");
        overallEditingContainer = (Gtk.Box)builder.GetObject("overallEditingContainer");
        treasureDataFrame = (Gtk.Widget)builder.GetObject("treasureDataFrame");
        treasureDataLabel = (Gtk.Label)builder.GetObject("treasureDataLabel");
        var quickstartToggleButton = (Gtk.ToggleToolButton)builder.GetObject("quickstartToggleButton");

        var linkImage = new Gdk.Pixbuf(Helper.GetResourceStream("LynnaLab.Resources.Link.png"));
        quickstartToggleButton.IconWidget = new Gtk.Image(linkImage);

        editTilesetButton = new Gtk.Button("Edit");
        editTilesetButton.Clicked += OnTilesetEditorButtonClicked;

        roomSpinButton = new SpinButtonHexadecimal();
        roomSpinButton.Digits = 3;
        objectgroupeditor1 = new ObjectGroupEditor();
        tilesetViewer1 = new TilesetViewer();
        roomeditor1 = new RoomEditor();
        worldMinimap = new HighlightingMinimap();
        dungeonMinimap = new Minimap();
        warpEditor = new WarpEditor(this);
        statusbar1 = new PriorityStatusbar();
        seasonComboBox = new ComboBoxFromConstants(showHelp: false);
        seasonComboBox.SpinButton.Adjustment.Upper = 3;

        ((Gtk.Box)builder.GetObject("roomSpinButtonHolder")).Add(roomSpinButton);
        ((Gtk.Box)builder.GetObject("objectGroupEditorHolder")).Add(objectgroupeditor1);
        ((Gtk.Box)builder.GetObject("tilesetViewerHolder")).Add(tilesetViewer1);
        ((Gtk.Box)builder.GetObject("roomEditorHolder")).Add(roomeditor1);
        ((Gtk.Box)builder.GetObject("worldMinimapHolder")).Add(worldMinimap);
        ((Gtk.Box)builder.GetObject("dungeonMinimapHolder")).Add(dungeonMinimap);
        ((Gtk.Box)builder.GetObject("warpEditorHolder")).Add(warpEditor);
        ((Gtk.Box)builder.GetObject("statusbarHolder")).Add(statusbar1);
        ((Gtk.Box)builder.GetObject("seasonComboBoxHolder")).Add(seasonComboBox);

        mainWindow.Title = "LynnaLab " + Helper.ReadResourceFile("LynnaLab.version.txt");

        roomeditor1.Scale = 2;
        roomeditor1.TilesetViewer = tilesetViewer1;
        roomeditor1.ObjectGroupEditor = objectgroupeditor1;
        roomeditor1.WarpEditor = warpEditor;

        eventGroup.Lock();


        // Event handlers from widgets

        roomSpinButton.ValueChanged += eventGroup.Add(OnRoomSpinButtonValueChanged);

        worldSpinButton.ValueChanged += eventGroup.Add(OnWorldSpinButtonValueChanged);
        dungeonSpinButton.ValueChanged += eventGroup.Add(OnDungeonSpinButtonValueChanged);
        floorSpinButton.ValueChanged += eventGroup.Add(OnFloorSpinButtonValueChanged);
        seasonComboBox.Changed += eventGroup.Add(OnSeasonComboBoxChanged);
        minimapNotebook.SwitchPage += new SwitchPageHandler(eventGroup.Add<SwitchPageArgs>(OnMinimapNotebookSwitchPage));
        contextNotebook.SwitchPage += new SwitchPageHandler(eventGroup.Add<SwitchPageArgs>(OnContextNotebookSwitchPage));

        roomeditor1.RoomChangedEvent += eventGroup.Add<RoomChangedEventArgs>((sender, args) =>
        {
            eventGroup.Lock();
            OnRoomChanged();

            // Only update minimap if the room editor did a "follow warp". Otherwise, we'll decide
            // whether to update the minimap from whatever code changed the room.
            if (args.fromFollowWarp)
            {
                UpdateMinimapFromRoom(args.fromFollowWarp);
                UpdateStatusBarWarnings();
            }

            eventGroup.Unlock();
        });

        dungeonMinimap.AddTileSelectedHandler(eventGroup.Add<int>(delegate (object sender, int index)
        {
            OnMinimapTileSelected(sender, dungeonMinimap.SelectedIndex);
        }));
        worldMinimap.AddTileSelectedHandler(eventGroup.Add<int>(delegate (object sender, int index)
        {
            OnMinimapTileSelected(sender, worldMinimap.SelectedIndex);
        }));

        tilesetViewer1.HoverChangedEvent += eventGroup.Add<int>((sender, tile) =>
        {
            if (tilesetViewer1.HoveringIndex != -1)
                statusbar1.Set((uint)StatusbarMessage.TileHovering,
                        "Hovering Tile: 0x" + tilesetViewer1.HoveringIndex.ToString("X2"));
            else
                statusbar1.RemoveAll((uint)StatusbarMessage.TileHovering);
        });
        tilesetViewer1.AddTileSelectedHandler(eventGroup.Add<int>(delegate (object sender, int index)
        {
            statusbar1.RemoveAll((uint)StatusbarMessage.TileHovering);
            statusbar1.Set((uint)StatusbarMessage.TileSelected, "Selected Tile: 0x" + index.ToString("X2"));
        }));

        roomeditor1.HoverChangedEvent += eventGroup.Add<int>((sender, tile) =>
        {
            if (roomeditor1.HoveringIndex != -1)
                statusbar1.Set((uint)StatusbarMessage.TileHovering, string.Format(
                        "Hovering Pos: {0},{1} (${1:X}{0:X})", roomeditor1.HoveringX, roomeditor1.HoveringY));
            else
                statusbar1.RemoveAll((uint)StatusbarMessage.TileHovering);
        });
        roomeditor1.WarpDestEditModeChangedEvent += eventGroup.Add<bool>((sender, activated) =>
        {
            if (activated)
                statusbar1.Set((uint)StatusbarMessage.WarpDestEditMode,
                        "Entered warp destination editing mode. To exit this mode, right-click on the warp destination and select \"Done\".");
            else
                statusbar1.RemoveAll((uint)StatusbarMessage.WarpDestEditMode);
        });
        // Default status bar message
        statusbar1.Set((uint)StatusbarMessage.TileSelected, "Selected Tile: 0x00");

        // Call this to initialize some state
        OnDarkenDungeonRoomsCheckboxToggled(null, null);


        // Event handlers from underlying data

        chestEventWrapper.Bind<ValueModifiedEventArgs>("ModifiedEvent", (sender, args) => UpdateChestData());
        chestEventWrapper.Bind<EventArgs>("DeletedEvent", (sender, args) => UpdateChestData());


        // Menu items
        Gtk.MenuItem shiftRoomMenuItem = new Gtk.MenuItem("Shift room");
        shiftRoomMenuItem.Submenu = new Gtk.Menu();

        Action<string, int, int> addShiftMenuItem = (label, x, y) =>
        {
            Gtk.MenuItem menuItem = new Gtk.MenuItem(label);
            menuItem.Activated += (s, e) =>
            {
                ActiveRoomLayout?.ShiftTiles(x, y);
            };
            (shiftRoomMenuItem.Submenu as Gtk.Menu).Append(menuItem);
        };

        addShiftMenuItem("Left", -1, 0);
        addShiftMenuItem("Right", 1, 0);
        addShiftMenuItem("Up", 0, -1);
        addShiftMenuItem("Down", 0, 1);

        (editMenuItem.Submenu as Gtk.Menu).Append(shiftRoomMenuItem);

        // Load "plugins"

        pluginCore = new PluginCore(this);
        LoadPlugins();

        mainWindow.ShowAll();

        eventGroup.UnlockAndClear();

        overallEditingContainer.Sensitive = false;
    }

    void LoadPlugins()
    {
        // TEMPORARY: Hide "Action" menu for now (AutoSmoother will be hidden until it's more
        // fleshed out)
        menubar1.Remove(actionMenuItem);

#if (DEBUG)
        Gtk.MenuItem gcMenuItem = new Gtk.MenuItem("Trigger GC");
        gcMenuItem.Activated += (s, e) =>
        {
            System.GC.Collect();
            Console.WriteLine("GC completed.");
        };
        (debugMenuItem.Submenu as Gtk.Menu).Append(gcMenuItem);
#else
        menubar1.Remove(debugMenuItem);
#endif

        pluginCore.ReloadPlugins();

        foreach (Plugin plugin in pluginCore.GetPlugins())
        {
            Gtk.Menu pluginSubMenu;
            if (plugin.Category == "Window")
                pluginSubMenu = editMenuItem.Submenu as Gtk.Menu;
            else if (plugin.Category == "Action")
                pluginSubMenu = actionMenuItem.Submenu as Gtk.Menu;
            else if (plugin.Category == "Debug")
                pluginSubMenu = debugMenuItem.Submenu as Gtk.Menu;
            else
            {
                log.Error("Unknown category '" + plugin.Category + "'.");
                continue;
            }

            var item = new MenuItem(plugin.Name);
            item.Activated += ((a, b) =>
            {
                plugin.SpawnWindow();
            });
            pluginSubMenu.Append(item);
        }
        menubar1.ShowAll();
    }

    void StartAnimations()
    {
        if (animationTimerID == 0)
            animationTimerID =
                GLib.Timeout.Add(1000 / 60, new GLib.TimeoutHandler(AnimationUpdater));
    }

    void EndAnimations()
    {
        if (animationTimerID != 0)
            GLib.Source.Remove(animationTimerID);
        animationTimerID = 0;

        var tileset = tilesetViewer1.Tileset;
        if (tileset != null)
            tileset.ResetAnimation();
    }

    bool AnimationUpdater()
    {
        var tileset = tilesetViewer1.Tileset;
        if (tileset == null)
            return true;
        IList<byte> changedTiles = tileset.UpdateAnimations(1);
        return true;
    }

    void OpenProject(string dir, string game = null)
    {
        ResponseType response = ResponseType.Yes;
        string mainFile = "ages.s";
        if (!File.Exists(dir + "/" + mainFile))
        {
            Gtk.MessageDialog d = new MessageDialog(
                mainWindow,
                DialogFlags.DestroyWithParent,
                MessageType.Warning,
                ButtonsType.YesNo,
                $"The folder you selected does not have a {mainFile} file. This probably indicates the folder does not contain the oracles disassembly. Attempt to continue anyway?");
            response = (ResponseType)d.Run();
            d.Dispose();
        }

        if (response == ResponseType.Yes)
        {
            CloseProject();

            // Try to load project config
            ProjectConfig config = ProjectConfig.Load(dir);

            // If not passed on commandline, get game from project config or prompt user
            if (game == null)
            {
                if (config.EditingGame != null && config.EditingGame != "")
                    game = config.EditingGame;
                else
                {
                    game = PromptForGame();
                    if (game != null && game.EndsWith("!"))
                    {
                        // Save selected game to file
                        game = game.Substring(0, game.Length-1);
                        config.SetEditingGame(game);
                    }
                }

                if (game == null)
                    return;
            }

            Project = new Project(dir, game, config);

            // Project has been loaded, update GUI stuff

            eventGroup.Lock();

            worldSpinButton.Adjustment = new Adjustment(0, 0, Project.NumGroups - 1, 1, 4, 0);
            dungeonSpinButton.Adjustment = new Adjustment(0, 0, Project.NumDungeons - 1, 1, 1, 0);
            roomSpinButton.Adjustment = new Adjustment(0, 0, Project.NumRooms - 1, 1, 16, 0);
            seasonComboBox.SetConstantsMapping(Project.SeasonMapping);
            seasonComboBox.ActiveValue = 0;

            if (Project.Game == Game.Ages)
                seasonComboBox.Hide();
            else
                seasonComboBox.Show();

            eventGroup.UnlockAndClear();

            SetWorld(0, 0);

            overallEditingContainer.Sensitive = true;
        }
    }

    /// Ask whether to open Ages or Seasons. Returns string "ages" or "seasons", with an exclamation
    /// mark if "remember my choice" was checked.
    string PromptForGame()
    {
        Gtk.Dialog d = new Gtk.Dialog("Choose Game", mainWindow, DialogFlags.Modal);
        d.AddButton("Ages", ResponseType.Yes);
        d.AddButton("Seasons", ResponseType.No);

        var checkbox = new Gtk.CheckButton("Remember my choice");
        d.ContentArea.Add(checkbox);

        d.SetSizeRequest(250, 0);

        d.ShowAll();
        var response = (ResponseType)d.Run();
        d.Dispose();

        string retval;
        if (response == ResponseType.Yes)
            retval = "ages";
        else if (response == ResponseType.No)
            retval = "seasons";
        else
            return null;

        return retval + (checkbox.Active ? "!" : "");
    }

    void SetTileset(Tileset tileset)
    {
        if (Project == null)
            return;
        if (tileset == tilesetViewer1.Tileset)
            return;

        eventGroup.Lock();

        tilesetViewer1.SetTileset(tileset);
        ActiveRoom.TilesetIndex = tileset.Index;

        if (tilesetModifiedEventWrapper == null)
        {
            tilesetModifiedEventWrapper = new WeakEventWrapper<ValueReferenceGroup>();
            tilesetModifiedEventWrapper.Bind<ValueModifiedEventArgs>("ModifiedEvent",
                    (sender, args) => UpdateStatusBarWarnings());
        }
        tilesetModifiedEventWrapper.ReplaceEventSource(tileset.GetValueReferenceGroup());

        eventGroup.Unlock();

        UpdateStatusBarWarnings();
    }
    void SetTileset(int index, int season)
    {
        if (Project == null)
            return;

        SetTileset(Project.GetTileset(index, season));
    }

    void UpdateStatusBarWarnings()
    {
        Action<StatusbarMessage, bool, string> setWarning = (type, active, message) =>
        {
            if (active)
                statusbar1.Set((uint)type, "WARNING: " + message);
            else
                statusbar1.RemoveAll((uint)type);
        };

        Tileset tileset = tilesetViewer1.Tileset;

        // Print warnings when tileset's layout group does not match expected value. Does nothing on
        // the hack-base branch as the tileset's layout group is ignored.
        if (!Project.Config.ExpandedTilesets)
        {
            int expectedGroup = Project.GetCanonicalLayoutGroup(ActiveRoom.Group, ActiveSeason);
            setWarning(StatusbarMessage.WrongLayoutGroup,
                       tileset.LayoutGroup != expectedGroup,
                       string.Format(
                        "Layout group of tileset ({0:X}) does not match expected value ({1:X})!"
                        + " This room's layout data might be shared with another's.",
                        tileset.LayoutGroup,
                        expectedGroup));
        }

        // Print warning if, on the dungeon tab, the tileset's dungeon index doesn't match the
        // dungeon itself
        setWarning(StatusbarMessage.DungeonIndexMismatch,
                   DungeonContextActive && tileset.DungeonIndex != dungeonSpinButton.ValueAsInt,
                   "Tileset's dungeon index doesn't match the dungeon");

        // Print warning if on the dungeon tab but tileset not marked as a dungeon
        setWarning(StatusbarMessage.DungeonBitMismatch,
                   DungeonContextActive && !tileset.IsDungeon,
                   "Tileset is not marked as a dungeon, but is used in a dungeon");

        // Print warning if tileset dungeon bit is unset, but dungeon value is not $F.
        // Only in Seasons, because Ages has better sanity checks for this.
        setWarning(StatusbarMessage.DungeonBitAndIndexMismatch,
                   Project.Game == Game.Seasons && !tileset.IsDungeon && tileset.DungeonIndex != 0x0f,
                   "If tileset is not a dungeon, dungeon index should be $F");

        // Print warning if tileset is a dungeon but used on an overworld
        setWarning(StatusbarMessage.DungeonOutsideDungeon,
                   WorldContextActive && tileset.IsDungeon && ActiveRoomLayout.IsSmall,
                   "Dungeon tileset being used outside a dungeon");

        // Warning for a room that's using a dungeon tileset but doesn't exist in that dungeon
        setWarning(StatusbarMessage.RoomNotInDungeon,
                   WorldContextActive && tileset.IsDungeon
                   && tileset.DungeonIndex < Project.NumDungeons
                   && !Project.GetDungeon(tileset.DungeonIndex).RoomUsed(ActiveRoom.Index),
                   $"Tileset is for dungeon {tileset.DungeonIndex} but this room is not in that dungeon");
    }

    // Called when room index (or season) is changed
    void OnRoomChanged()
    {
        if (ActiveRoom == null)
            return;

        eventGroup.Lock();

        roomSpinButton.Value = ActiveRoom.Index;
        warpEditor.SetMap(ActiveRoom.Index >> 8, ActiveRoom.Index & 0xff);
        SetTileset(ActiveRoomLayout.Tileset);

        if (roomVre == null)
        {
            // This only runs once
            roomVre = new ValueReferenceEditor(ActiveRoom.ValueReferenceGroup);
            roomVre.AddWidgetToRight("Tileset", editTilesetButton);
            roomVreHolder.Add(roomVre);
            roomVre.ShowAll();

            roomTilesetModifiedEventWrapper.Bind<ValueModifiedEventArgs>("ModifiedEvent",
                    (sender, args) => SetTileset((sender as ValueReference).GetIntValue(), ActiveSeason));
        }
        else
        {
            roomVre.ReplaceValueReferenceGroup(ActiveRoom.ValueReferenceGroup);
        }

        // Watch for changes to this room's tileset
        roomTilesetModifiedEventWrapper.ReplaceEventSource(ActiveRoom.ValueReferenceGroup["Tileset"]);

        // Watch for changes to the chest

        UpdateChestData();

        eventGroup.Unlock();

        UpdateStatusBarWarnings();
    }

    void UpdateChestData()
    {
        Chest chest = ActiveRoom.Chest;

        if (chest != null)
        {
            ValueReferenceGroup vrg = ActiveRoom.Chest.ValueReferenceGroup;
            if (chestVre == null)
            {
                chestVre = new ValueReferenceEditor(vrg);
                chestVreHolder.Add(chestVre);
            }
            else
            {
                chestVre.ReplaceValueReferenceGroup(vrg);
            }

            TreasureObject treasure = chest.Treasure;

            if (treasure == null)
            {
                nonExistentTreasureHolder.ShowAll();
                treasureVreHolder.Hide();

                if (chest.TreasureID >= Project.NumTreasures)
                    treasureDataFrame.Hide();
                else
                    treasureDataFrame.Show();
            }
            else
            {
                nonExistentTreasureHolder.Hide();
                treasureDataFrame.Show();
                treasureVreHolder.Show();

                if (treasureVre == null)
                {
                    treasureVre = new ValueReferenceEditor(treasure.ValueReferenceGroup);
                    treasureVreHolder.Add(treasureVre);
                }
                else
                {
                    treasureVre.ReplaceValueReferenceGroup(treasure.ValueReferenceGroup);
                }
            }

            treasureDataLabel.Text = string.Format("Treasure ${0:X2} Subid ${1:X2} Data",
                    vrg.GetIntValue("ID"), vrg.GetIntValue("SubID"));
        }

        if (chest == null)
        {
            chestEditorBox.Hide();
            treasureDataFrame.Hide();
            chestAddHolder.ShowAll();
        }
        else
        {
            chestEditorBox.ShowAll();
            chestAddHolder.Hide();
        }

        chestEventWrapper.ReplaceEventSource(chest);
    }

    public void UpdateMinimapFromRoom(bool changeWorldDungeonTab)
    {
        eventGroup.Lock();

        int x, y, floor;
        Dungeon dungeon = Project.GetRoomDungeon(ActiveRoom, out x, out y, out floor);

        bool toDungeonTab;

        // If true, we switch to the dungeon tab if the room is there and vice-versa; if false, we
        // stay on the tab we're on already.
        if (changeWorldDungeonTab)
            toDungeonTab = dungeon != null;
        else
            toDungeonTab = ActiveMap is Dungeon;

        if (toDungeonTab)
        {
            if (dungeon != null)
            {
                ActiveMap = dungeon;

                ActiveMinimap.Floor = floor;
                ActiveMinimap.SelectedIndex = x + y * ActiveMinimap.Width;
            }
        }
        else
        {
            Room r = ActiveRoom;
            Map map = Project.GetWorldMap(ActiveRoom.Group, ActiveSeason);
            ActiveMap = map;
            ActiveMinimap.SelectedIndex = r.Index & 0xff;
        }

        UpdateMapSpinButtons();

        eventGroup.UnlockAndClear();
    }

    void SetRoom(Room room, int season)
    {
        roomeditor1.SetRoom(room, season);

        // roomeditor1's changed event handler will fire, which in turn invokes "OnRoomChanged", so
        // don't call that here.
    }

    void OnMinimapTileSelected(object sender, int index)
    {
        SetRoom(ActiveMinimap.GetRoom(), ActiveSeason);
    }

    void UpdateMapSpinButtons()
    {
        if (ActiveMap is Dungeon)
        {
            Dungeon dungeon = ActiveMap as Dungeon;
            dungeonSpinButton.Value = dungeon.Index;
            floorSpinButton.Adjustment = new Adjustment(ActiveMinimap.Floor, 0, dungeon.NumFloors - 1, 1, 0, 0);
        }
        else
        {
            worldSpinButton.Value = ActiveMap.MainGroup;
        }
    }

    void OnMapChanged()
    {
        if (ActiveMap == null)
            return;

        eventGroup.Lock();
        UpdateMapSpinButtons();
        eventGroup.UnlockAndClear();

        SetRoom(ActiveMinimap.GetRoom(), ActiveMap.Season);
    }

    void SetWorld(WorldMap map)
    {
        if (Project == null)
            return;
        ActiveMap = map;
    }
    void SetWorld(int index, int season)
    {
        if (Project == null)
            return;
        SetWorld(Project.GetWorldMap(index, season));
    }

    // This returns ResponseType.Yes, No, or Cancel
    ResponseType AskSave(string info)
    {
        if (Project == null)
            return ResponseType.No;

        ResponseType response;
        Gtk.Dialog d = new Dialog("Save Project?", mainWindow,
                DialogFlags.DestroyWithParent,
                Gtk.Stock.Yes, ResponseType.Yes,
                Gtk.Stock.No, ResponseType.No,
                Gtk.Stock.Cancel, ResponseType.Cancel);
        Gtk.Label infoLabel = new Gtk.Label(info);
        infoLabel.MarginBottom = 6;
        d.ContentArea.Add(infoLabel);
        d.ShowAll();
        response = (ResponseType)d.Run();
        d.Dispose();
        if (response == ResponseType.Yes)
        {
            Project.Save();
        }

        return response;
    }

    void AskQuit()
    {
        ResponseType r = AskSave("Save project before exiting?");
        if (r == ResponseType.Yes || r == ResponseType.No)
            Quit();
    }

    void Quit()
    {
        CloseProject();
        globalConfig.Save();
        Application.Quit();
    }

    void CloseProject()
    {
        if (Project != null)
        {
            QuickstartData.enabled = false;
            SetRoom(null, 0);
            worldMinimap.SetMap(null);
            dungeonMinimap.SetMap(null);
            Project.Close();
            Project = null;

            overallEditingContainer.Sensitive = false;

            // Some more stuff that ought to be done here:
            // - Close all popup windows (tileset editor, dungeon editor)
            //
            // Ultimately some dangling pointers to the old Project will probably remain until
            // another project replaces it, which is no big deal, as long as nothing from the old
            // state leaks into the UI when the new project is loaded.
        }
    }

    protected void OnDeleteEvent(object sender, DeleteEventArgs a)
    {
        AskQuit();
        a.RetVal = true;
    }

    protected void OnOpenActionActivated(object sender, EventArgs e)
    {
        Gtk.FileChooserDialog dialog = new FileChooserDialog("Select the oracles disassembly base directory",
                mainWindow,
                FileChooserAction.SelectFolder,
                "Cancel", ResponseType.Cancel,
                "Select Folder", ResponseType.Accept);
        dialog.LocalOnly = false;
        ResponseType response = (ResponseType)dialog.Run();

        if (response == ResponseType.Accept)
        {
            ResponseType r2 = AskSave("Save project before closing it?");
            if (r2 != ResponseType.Cancel)
            {
                string basedir = dialog.Filename;
                OpenProject(basedir);
            }
        }
        dialog.Dispose();
    }

    protected void OnSaveActionActivated(object sender, EventArgs e)
    {
        if (Project != null)
            Project.Save();
    }

    /// Create a dialog box which shows the build output, then runs the emulator
    protected void OnRunActionActivated(object sender, EventArgs e)
    {
        if (Project == null)
            return;

        Project.Save();

        if (buildDialog != null)
        {
            buildDialog.Destroy();
        }

        buildDialog = new BuildDialog(this);
        buildDialog.ShowAll();
    }

    protected void OnPromptForEmulatorActionActivated(object sender, EventArgs e)
    {
        var cmd = PromptForEmulator();
        if (cmd != null)
        {
            globalConfig.EmulatorCommand = cmd;
            globalConfig.Save();
        }
    }

    protected void OnCloseActionActivated(object sender, EventArgs e)
    {
        if (AskSave("Save project before closing?") != ResponseType.Cancel)
        {
            CloseProject();
        }
    }

    protected void OnAnimationsActionActivated(object sender, EventArgs e)
    {
        if ((sender as Gtk.CheckMenuItem).Active)
            StartAnimations();
        else
            EndAnimations();
    }

    protected void OnQuitActionActivated(object sender, EventArgs e)
    {
        AskQuit();
    }

    protected void OnSwitchGameActionActivated(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        if (AskSave("Save project before switching?") != ResponseType.Cancel)
        {
            string directory = Project.BaseDirectory;
            string game = Project.GameString == "ages" ? "seasons" : "ages";
            CloseProject();
            OpenProject(directory, game);
        }
    }

    protected void OnReloadActionActivated(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        if (AskSave("Save project before reloading?") != ResponseType.Cancel)
        {
            string directory = Project.BaseDirectory;
            string game = Project.GameString;
            CloseProject();
            OpenProject(directory, game);
        }
    }

    protected void OnDungeonSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        dungeonMinimap.SetMap(Project.GetDungeon(dungeonSpinButton.ValueAsInt));
        OnMapChanged();
    }

    protected void OnFloorSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        int floor = (sender as Gtk.SpinButton).ValueAsInt;
        if (ActiveMap is Dungeon && floor >= (ActiveMap as Dungeon).NumFloors)
            return;
        dungeonMinimap.Floor = floor;
        OnMapChanged();
    }

    protected void OnWorldSpinButtonValueChanged(object sender, EventArgs e)
    {
        changingWorld = true;
        UpdateWorld();
        changingWorld = false;
    }

    protected void OnSeasonComboBoxChanged(object sender, EventArgs e)
    {
        UpdateWorld();
    }

    void UpdateWorld()
    {
        if (Project == null)
            return;
        int index = worldSpinButton.ValueAsInt;
        WorldMap world = Project.GetWorldMap(index, ActiveSeason);
        if (ActiveMap != world)
            SetWorld(world);
    }

    protected void OnMinimapNotebookSwitchPage(object o, SwitchPageArgs args)
    {
        if (Project == null)
            return;
        Notebook nb = minimapNotebook;
        if (nb.Page == 0)
        {
            int world = worldSpinButton.ValueAsInt;
            ActiveMinimap.SetMap(Project.GetWorldMap(world, ActiveSeason));
        }
        else if (nb.Page == 1)
            ActiveMinimap.SetMap(Project.GetDungeon(dungeonSpinButton.ValueAsInt));
        OnMapChanged();
    }

    protected void OnContextNotebookSwitchPage(object o, SwitchPageArgs args)
    {
        UpdateRoomEditorViews();
    }

    void UpdateRoomEditorViews()
    {
        bool viewObjects = viewObjectsCheckButton.Active;
        bool viewWarps = viewWarpsCheckButton.Active;

        roomeditor1.EnableTileEditing = RoomContextActive;

        if (ObjectContextActive)
        { // Object editor
            viewObjects = true;
        }
        if (WarpContextActive)
        { // Warp editor
            viewWarps = true;
        }

        roomeditor1.ViewObjects = viewObjects;
        roomeditor1.ViewWarps = viewWarps;
        roomeditor1.ViewChests = ChestContextActive;
    }

    protected void OnRoomSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        SpinButton button = sender as SpinButton;

        Room room = Project.GetIndexedDataType<Room>(button.ValueAsInt);
        if ((ActiveMap is Dungeon || AutoAdjustGroup) && room.ExpectedIndex != button.ValueAsInt)
        {
            // "Correct" the room value by looking for the "expected" version of the room (ie.
            // sidescrolling rooms have duplicates, only one is the "correct" version).
            button.Value = room.ExpectedIndex;
            return; // Callback will get invoked again
        }

        room = Project.GetIndexedDataType<Room>(button.ValueAsInt);
        if (room != ActiveRoom)
        {
            SetRoom(room, ActiveSeason);
            UpdateMinimapFromRoom(false);
        }
    }

    protected void OnTilesetEditorButtonClicked(object sender, EventArgs e)
    {
        if (Project == null)
            return;

        var tilesetEditorWindow = new Gtk.Window(Gtk.WindowType.Toplevel);
        tilesetEditorWindow.Title = "Tileset Editor";
        TilesetEditor a = new TilesetEditor(this, tilesetViewer1.Tileset);
        tilesetEditorWindow.Add(a);

        // I get weird errors and crashes when this Destroyed handler is not
        // present. In theory the garbage collector should be able to handle
        // this, but in practice it seems like Gtk gets very unhappy when a
        // closed window is not Dispose'd immediately.
        tilesetEditorWindow.Destroyed += (sender, e) => {
            (sender as Gtk.Window)?.Dispose();
        };

        tilesetEditorWindow.ShowAll();
    }

    protected void OnViewObjectsCheckBoxToggled(object sender, EventArgs e)
    {
        roomeditor1.ViewObjects = (sender as Gtk.CheckButton).Active;
        roomeditor1.QueueDraw();
    }

    protected void OnViewWarpsCheckBoxToggled(object sender, EventArgs e)
    {
        roomeditor1.ViewWarps = (sender as Gtk.CheckButton).Active;
        roomeditor1.QueueDraw();
    }

    protected void OnDarkenDungeonRoomsCheckboxToggled(object sender, EventArgs e)
    {
        worldMinimap.DarkenUsedDungeonRooms = darkenDungeonRoomsCheckbox.Active;
    }

    protected void OnQuickstartToggled(object sender, EventArgs e)
    {
        if (Project == null)
            return;

        var button = sender as Gtk.ToggleToolButton;

        QuickstartStateChanged(button.Active);
    }

    protected void OnCopyRoomLayoutActivated(object sender, EventArgs e)
    {
        if (ActiveRoomLayout == null)
            return;
        copiedLayoutData = ActiveRoomLayout.GetLayout();
        copiedLayoutWidth = ActiveRoomLayout.Width;
    }

    protected void OnPasteRoomLayoutActivated(object sender, EventArgs e)
    {
        if (copiedLayoutData == null || ActiveRoomLayout == null)
            return;

        if (copiedLayoutWidth != ActiveRoomLayout.Width)
        {
            using (var dialog = new Gtk.MessageDialog(
                       mainWindow,
                       DialogFlags.DestroyWithParent,
                       MessageType.Error,
                       ButtonsType.Ok,
                       "Room size mismatch."
                   ))
            {
                dialog.Run();
            }
            return;
        }

        var d = new Gtk.MessageDialog(
            mainWindow,
            DialogFlags.DestroyWithParent,
            MessageType.Info,
            ButtonsType.YesNo,
            $"This will overwrite the contents of the current room. Proceed?");
        var response = (ResponseType)d.Run();
        d.Dispose();

        if (response != Gtk.ResponseType.Yes)
            return;

        ActiveRoomLayout.SetLayout(copiedLayoutData);
    }

    void QuickstartStateChanged(bool active)
    {
        QuickstartData.enabled = active;
        if (active)
        {
            QuickstartData.group = (byte)ActiveRoom.Group;
            QuickstartData.room = (byte)(ActiveRoom.Index & 0xff);
            QuickstartData.season = (byte)ActiveSeason;
            QuickstartData.x = 0x48;
            QuickstartData.y = 0x48;
        }

        roomeditor1.OnQuickstartModified();
    }


    protected void OnAddChestButtonClicked(object sender, EventArgs e)
    {
        ActiveRoom?.AddChest();
        UpdateChestData();
    }

    protected void OnRemoveChestButtonClicked(object sender, EventArgs e)
    {
        ActiveRoom?.DeleteChest();
        // Chest's deleted handler will invoke UpdateChestData()
    }

    protected void OnCreateTreasureButtonClicked(object sender, EventArgs e)
    {
        if (ActiveRoom?.Chest == null)
            return;
        TreasureObject t = ActiveRoom.Chest.TreasureGroup.AddTreasureObjectSubid();
        if (t != null)
        {
            ActiveRoom.Chest.Treasure = t;
            // Must call this explicitly since it normally only gets invoked when the chest's
            // treasure index changes. In this case the treasure index may stay the same even if
            // that index was only just created now.
            UpdateChestData();
        }
    }

    protected void OnRedrawMinimapButtonClicked(object sender, EventArgs e)
    {
        worldMinimap.InvalidateImageCache();
        dungeonMinimap.InvalidateImageCache();
    }

    void OnWindowClosed(object sender, DeleteEventArgs e)
    {
        AskQuit();
        e.RetVal = true; // Event is "handled". This prevents the window closure.
    }


    // Public methods

    /// Returns file selected, or null if nothing selected
    public string PromptForEmulator(bool infoPrompt = false)
    {
        if (infoPrompt)
        {
            var d = new Gtk.MessageDialog(
                mainWindow,
                DialogFlags.DestroyWithParent,
                MessageType.Info,
                ButtonsType.OkCancel,
                $"Your gameboy emulator path has not been configured. Select your emulator executable file now to run {Project.GameString}.gbc.");
            var response = (ResponseType)d.Run();
            d.Dispose();

            if (response != Gtk.ResponseType.Ok)
                return null;
        }

        using (var fileDialog = new Gtk.FileChooserDialog(
            "Select your emulator executable file",
            mainWindow,
            FileChooserAction.Open,
            "Cancel", ResponseType.Cancel,
            "Select File", ResponseType.Accept))
        {

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var exeFilter = new Gtk.FileFilter();
                var allFilter = new Gtk.FileFilter();
                exeFilter.Name = "Executable files (*.exe)";
                allFilter.Name = "All files";
                exeFilter.AddPattern("*.exe");
                allFilter.AddPattern("*");
                fileDialog.AddFilter(exeFilter);
                fileDialog.AddFilter(allFilter);
            }
            var response = (ResponseType)fileDialog.Run();

            if (response == ResponseType.Accept)
            {
                return fileDialog.Filename + " | {GAME}.gbc";
            }
        }

        return null;
    }

    public void RegisterEmulatorProcess(Process process)
    {
        // Kill existing emulator process if it exists.
        // Could use CloseMainWindow() instead to ask more nicely, but not guaranteed to work.
        if (emulatorProcess != null)
        {
            emulatorProcess.Kill(true); // Pass true to kill whole process tree
            emulatorProcess.Close();
        }
        emulatorProcess = process;
    }
}
