using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Linq;
using Gtk;
using LynnaLab;

// Doesn't extend the Gtk.Window class because the actual window is defined in Glade
// (Glade/MainWindow.ui).
public class MainWindow
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    // GUI stuff
    Gtk.Window mainWindow;

	Gtk.MenuBar menubar1;
    Gtk.MenuItem editMenuItem, actionMenuItem, debugMenuItem;

	Gtk.Notebook minimapNotebook;
	Gtk.SpinButton worldSpinButton;
	Gtk.CheckButton darkenDungeonRoomsCheckbox;
	LynnaLab.HighlightingMinimap worldMinimap;
	Gtk.SpinButton dungeonSpinButton;
	Gtk.SpinButton floorSpinButton;
	LynnaLab.Minimap dungeonMinimap;

	Gtk.Statusbar statusbar1;

	LynnaLab.SpinButtonHexadecimal roomSpinButton, tilesetSpinButton;
    ComboBoxFromConstants musicComboBox;
	LynnaLab.ObjectGroupEditor objectgroupeditor1;
	LynnaLab.TilesetViewer tilesetViewer1;
	LynnaLab.RoomEditor roomeditor1;

    WarpEditor warpEditor = null;

    // Variables
    uint animationTimerID = 0;
    PluginCore pluginCore;


    // Properties

    internal Project Project { get; set; }
    internal Room ActiveRoom {
        get {
            return roomeditor1.Room;
        }
    }
    internal Map ActiveMap {
        get {
            return minimapNotebook.Page == 0 ? worldMinimap.Map : dungeonMinimap.Map;
        }
    }
    internal int MapSelectedX {
        get {
            return minimapNotebook.Page == 0 ? worldMinimap.SelectedX : dungeonMinimap.SelectedX;
        }
    }
    internal int MapSelectedY {
        get {
            return minimapNotebook.Page == 0 ? worldMinimap.SelectedY : dungeonMinimap.SelectedY;
        }
    }
    internal int MapSelectedFloor {
        get {
            return minimapNotebook.Page == 0 ? worldMinimap.Floor : dungeonMinimap.Floor;
        }
    }

    internal MainWindow() : this("") {}
    internal MainWindow (string directory)
    {
        log.Debug("Beginning Program");

        Gtk.Builder builder = new Builder();
        builder.AddFromString(Helper.ReadResourceFile("LynnaLab.Glade.MainWindow.ui"));
        builder.Autoconnect(this);

        mainWindow = (builder.GetObject("mainWindow") as Gtk.Window);

        menubar1 = (Gtk.MenuBar)builder.GetObject("menubar1");
        editMenuItem = (Gtk.MenuItem)builder.GetObject("editMenuItem");
        actionMenuItem = (Gtk.MenuItem)builder.GetObject("actionMenuItem");
        debugMenuItem = (Gtk.MenuItem)builder.GetObject("debugMenuItem");

        minimapNotebook = (Gtk.Notebook)builder.GetObject("minimapNotebook");
        worldSpinButton = (Gtk.SpinButton)builder.GetObject("worldSpinButton");
        darkenDungeonRoomsCheckbox = (Gtk.CheckButton)builder.GetObject("darkenDungeonRoomsCheckbox");
        dungeonSpinButton = (Gtk.SpinButton)builder.GetObject("dungeonSpinButton");
        floorSpinButton = (Gtk.SpinButton)builder.GetObject("floorSpinButton");
        statusbar1 = (Gtk.Statusbar)builder.GetObject("statusbar1");

        roomSpinButton = new SpinButtonHexadecimal(0, 0x5ff);
        roomSpinButton.Digits = 3;
        roomSpinButton.ValueChanged += OnRoomSpinButtonValueChanged;
        tilesetSpinButton = new SpinButtonHexadecimal();
        tilesetSpinButton.Digits = 2;
        tilesetSpinButton.ValueChanged += OnTilesetSpinButtonValueChanged;

        musicComboBox = new ComboBoxFromConstants();
        musicComboBox.Changed += OnMusicComboBoxChanged;

        objectgroupeditor1 = new ObjectGroupEditor();
        tilesetViewer1 = new TilesetViewer();
        roomeditor1 = new RoomEditor();
        worldMinimap = new HighlightingMinimap();
        dungeonMinimap = new Minimap();
        warpEditor = new WarpEditor(this);

        ((Gtk.Box)builder.GetObject("roomSpinButtonHolder")).Add(roomSpinButton);
        ((Gtk.Box)builder.GetObject("tilesetSpinButtonHolder")).Add(tilesetSpinButton);
        ((Gtk.Box)builder.GetObject("musicComboBoxHolder")).Add(musicComboBox);
        ((Gtk.Box)builder.GetObject("objectGroupEditorHolder")).Add(objectgroupeditor1);
        ((Gtk.Box)builder.GetObject("tilesetViewerHolder")).Add(tilesetViewer1);
        ((Gtk.Box)builder.GetObject("roomEditorHolder")).Add(roomeditor1);
        ((Gtk.Box)builder.GetObject("worldMinimapHolder")).Add(worldMinimap);
        ((Gtk.Box)builder.GetObject("dungeonMinimapHolder")).Add(dungeonMinimap);
        ((Gtk.Box)builder.GetObject("warpEditorHolder")).Add(warpEditor);

        roomeditor1.TilesetViewer = tilesetViewer1;
        roomeditor1.ObjectGroupEditor = objectgroupeditor1;
        roomeditor1.WarpEditor = warpEditor;
        dungeonMinimap.AddTileSelectedHandler(delegate(object sender, int index) {
            Room room = dungeonMinimap.GetRoom();
            SetRoom(room);
        });
        worldMinimap.AddTileSelectedHandler(delegate(object sender, int index) {
            Room room = worldMinimap.GetRoom();
            SetRoom(room);
        });

        tilesetViewer1.HoverChangedEvent += delegate() {
            if (tilesetViewer1.HoveringIndex == -1)
                statusbar1.Push(1,
                        "Selected Tile: 0x" + tilesetViewer1.SelectedIndex.ToString("X2"));
            else
                statusbar1.Push(1,
                        "Hovering Tile: 0x" + tilesetViewer1.HoveringIndex.ToString("X2"));
        };
        tilesetViewer1.AddTileSelectedHandler(delegate(object sender, int index) {
            statusbar1.Push(1,
                    "Selected Tile: 0x" + index.ToString("X2"));
        });

        roomeditor1.HoverChangedEvent += delegate() {
            if (roomeditor1.HoveringIndex == -1)
                statusbar1.Push(1,
                        "Selected Tile: 0x" + tilesetViewer1.SelectedIndex.ToString("X2"));
            else
                statusbar1.Push(2,
                        "Hovering Tile: (" + roomeditor1.HoveringX +
                        ", " + roomeditor1.HoveringY + ")");
        };

        worldSpinButton.Adjustment = new Adjustment(0, 0, 5, 1, 0, 0);
        dungeonSpinButton.Adjustment = new Adjustment(0, 0, 15, 1, 0, 0);

        OnDarkenDungeonRoomsCheckboxToggled(null, null);


        pluginCore = new PluginCore(this);

        LoadPlugins();

        if (directory != "")
            OpenProject(directory);

        mainWindow.ShowAll();
    }

    void LoadPlugins() {
        pluginCore.ReloadPlugins();

        foreach (Plugin plugin in pluginCore.GetPlugins()) {
            Gtk.Menu pluginSubMenu;
            if (plugin.Category == "Window")
                pluginSubMenu = editMenuItem.Submenu as Gtk.Menu;
            else if (plugin.Category == "Action")
                pluginSubMenu = actionMenuItem.Submenu as Gtk.Menu;
            else if (plugin.Category == "Debug")
                pluginSubMenu = debugMenuItem.Submenu as Gtk.Menu;
            else {
                log.Error("Unknown category '" + plugin.Category + "'.");
                continue;
            }

            var item = new MenuItem(plugin.Name);
            item.Activated += ((a, b) =>
                    {
                    plugin.Clicked();
                    });
            pluginSubMenu.Append(item);
        }
        menubar1.ShowAll();
    }

    void StartAnimations() {
        if (animationTimerID == 0)
            animationTimerID =
                GLib.Timeout.Add(1000/60, new GLib.TimeoutHandler(AnimationUpdater));
    }

    void EndAnimations() {
        if (animationTimerID != 0)
            GLib.Source.Remove(animationTimerID);
        animationTimerID = 0;
    }

    bool AnimationUpdater() {
        var tileset = tilesetViewer1.Tileset;
        if (tileset == null)
            return true;
        IList<byte> changedTiles = tileset.UpdateAnimations(1);
        return true;
    }

    void OpenProject(string dir) {
        ResponseType response = ResponseType.Yes;
        string mainFile = "ages.s";
        if (!File.Exists(dir + "/" + mainFile)) {
            Gtk.MessageDialog d = new MessageDialog(mainWindow,
                    DialogFlags.DestroyWithParent,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    "The folder you selected does not have a " + mainFile + " file. This probably indicates the folder does not contain the ages disassembly. Attempt to continue anyway?");
            response = (ResponseType)d.Run();
            d.Dispose();
        }

        if (response == ResponseType.Yes) {
            if (Project != null) {
                Project.Close();
                Project = null;
            }
            Project = new Project(dir);

            /*
            try {
                Project = new Project(dir);
            }
            catch (Exception ex) {
                string outputString = "The following error was encountered while opening the Project:\n\n";
                outputString += ex.Message;

                Gtk.MessageDialog d = new MessageDialog(this,
                                         DialogFlags.DestroyWithParent,
                                         MessageType.Error,
                                         ButtonsType.Ok,
                                         outputString);
                d.Run();
                d.Dispose();
            }
    */

            musicComboBox.SetConstantsMapping(Project.MusicMapping);
            SetWorld(0);
        }
    }

    void SetTileset(Tileset tileset) {
        if (Project == null)
            return;
        tilesetViewer1.SetTileset(tileset);
        tilesetSpinButton.Value = tileset.Index;
        roomeditor1.Room.SetTileset(tileset);
        roomeditor1.QueueDraw();
    }

    public void SetRoom(int room) {
        if (Project == null)
            return;
        SetRoom(Project.GetIndexedDataType<Room>(room));
    }

    public void SetRoom(Room room) {
        if (Project == null)
            return;
        roomeditor1.SetRoom(room);
        SetTileset(room.Tileset);
        roomSpinButton.Value = room.Index;
        musicComboBox.Active = Project.MusicMapping.IndexOf((byte)room.GetMusicID());
        warpEditor.SetMap(room.Index>>8, room.Index&0xff);
    }

    void SetDungeon(Dungeon dungeon) {
        if (Project == null)
            return;
        dungeonSpinButton.Value = dungeon.Index;
        floorSpinButton.Value = 0;
        floorSpinButton.Adjustment = new Adjustment(0, 0, dungeon.NumFloors-1, 1, 0, 0);
        dungeonMinimap.SetMap(dungeon);
        SetRoom(dungeonMinimap.GetRoom());
    }
    void SetDungeon(int index) {
        if (Project == null)
            return;
        SetDungeon(Project.GetIndexedDataType<Dungeon>(index));
    }
    void SetWorld(WorldMap map) {
        if (Project == null)
            return;
        worldSpinButton.Value = map.Index;
        worldMinimap.SetMap(map);
        SetRoom(worldMinimap.GetRoom());
    }
    void SetWorld(int index) {
        if (Project == null)
            return;
        SetWorld(Project.GetIndexedDataType<WorldMap>(index));
    }

    // This returns ResponseType.Yes, No, or Cancel
    ResponseType AskSave(string info) {
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
        if (response == ResponseType.Yes) {
            Project.Save();
        }

        return response;
    }

    void AskQuit() {
        ResponseType r = AskSave("Save project before exiting?");
        if (r == ResponseType.Yes || r == ResponseType.No)
            Quit();
    }

    void Quit() {
        if (Project != null)
            Project.Close();
        Application.Quit();
    }

    protected void OnDeleteEvent (object sender, DeleteEventArgs a)
    {
        AskQuit();
        a.RetVal = true;
    }

    protected void OnOpenActionActivated(object sender, EventArgs e)
    {
        Gtk.FileChooserDialog dialog = new FileChooserDialog("Select the ages disassembly base directory",
                mainWindow,
                FileChooserAction.SelectFolder,
                "Cancel", ResponseType.Cancel,
                "Select Folder", ResponseType.Accept);
        ResponseType response = (ResponseType)dialog.Run();

        if (response == ResponseType.Accept) {
            ResponseType r2 = AskSave("Save project before closing it?");
            if (r2 != ResponseType.Cancel) {
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

    protected void OnAnimationsActionActivated(object sender, EventArgs e) {
        if ((sender as Gtk.CheckMenuItem).Active)
            StartAnimations();
        else
            EndAnimations();
    }

    protected void OnQuitActionActivated(object sender, EventArgs e)
    {
        AskQuit();
    }

    protected void OnDungeonSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        GLib.Idle.Add(new GLib.IdleHandler(delegate() {
                    SetDungeon(Project.GetIndexedDataType<Dungeon>(dungeonSpinButton.ValueAsInt));
                    return false;
        }));
    }

    protected void OnFloorSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        GLib.Idle.Add(new GLib.IdleHandler(delegate() {
                    dungeonMinimap.Floor = floorSpinButton.ValueAsInt;
                    SetRoom(dungeonMinimap.GetRoom());
                    return false;
        }));
    }

    protected void OnWorldSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        GLib.Idle.Add(new GLib.IdleHandler(delegate() {
                    SetWorld(worldSpinButton.ValueAsInt);
                    return false;
        }));
    }

    protected void OnMinimapNotebookSwitchPage(object o, SwitchPageArgs args)
    {
        if (Project == null)
            return;
        GLib.Idle.Add(new GLib.IdleHandler(delegate() {
                    Notebook nb = minimapNotebook;
                    if (nb.Page == 0)
                        SetWorld(worldSpinButton.ValueAsInt);
                    else if (nb.Page == 1)
                        SetDungeon(dungeonSpinButton.ValueAsInt);
                    return false;
        }));
    }

    protected void OnRoomSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        SpinButton button = sender as SpinButton;
        SetRoom(Project.GetIndexedDataType<Room>(button.ValueAsInt));
    }

    protected void OnTilesetSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        SpinButton button = sender as SpinButton;
        SetTileset(Project.GetIndexedDataType<Tileset>(button.ValueAsInt));
    }


    protected void OnMusicComboBoxChanged(object sender, EventArgs e) {
        if (Project == null)
            return;
        roomeditor1.Room.SetMusicID(Project.MusicMapping.StringToByte(musicComboBox.ActiveId));
    }

    protected void OnTilesetEditorButtonClicked(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        Window win = new Window(WindowType.Toplevel);
        TilesetEditor a = new TilesetEditor(tilesetViewer1.Tileset);
        win.Add(a);
        win.Title = "Edit Tileset";
        win.ShowAll();
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

    void OnWindowClosed(object sender, DeleteEventArgs e) {
        AskQuit();
        e.RetVal = true; // Event is "handled". This prevents the window closure.
    }
}
