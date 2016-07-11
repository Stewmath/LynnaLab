using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Linq;
using Gtk;
using LynnaLab;

public partial class MainWindow: Gtk.Window
{
    private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    uint animationTimerID;
    PluginCore pluginCore;

    Gtk.Menu pluginSubMenu;

    internal Project Project { get; set; }
    internal Room ActiveRoom {
        get {
            return roomeditor1.Room;
        }
    }
    internal Map ActiveMap {
        get {
            return notebook2.Page == 0 ? worldMinimap.Map : dungeonMinimap.Map;
        }
    }
    internal int MapSelectedX {
        get {
            return notebook2.Page == 0 ? worldMinimap.SelectedX : dungeonMinimap.SelectedX;
        }
    }
    internal int MapSelectedY {
        get {
            return notebook2.Page == 0 ? worldMinimap.SelectedY : dungeonMinimap.SelectedY;
        }
    }
    internal int MapSelectedFloor {
        get {
            return notebook2.Page == 0 ? worldMinimap.Floor : dungeonMinimap.Floor;
        }
    }

    internal MainWindow() : this("") {}
    internal MainWindow (string directory) : base (Gtk.WindowType.Toplevel)
    {
        log.Debug("Beginning Program");

        Build();

        roomeditor1.SetClient(areaviewer1);
        roomeditor1.SetObjectGroupEditor(objectgroupeditor1);
        dungeonMinimap.TileSelectedEvent += delegate(object sender) {
            Room room = dungeonMinimap.GetRoom();
            SetRoom(room);
        };
        worldMinimap.TileSelectedEvent += delegate(object sender) {
            Room room = worldMinimap.GetRoom();
            SetRoom(room);
        };

        areaviewer1.HoverChangedEvent += delegate() {
            if (areaviewer1.HoveringIndex == -1)
                statusbar1.Push(1,
                        "Selected Tile: 0x" + areaviewer1.SelectedIndex.ToString("X2")); 
            else
                statusbar1.Push(1,
                        "Hovering Tile: 0x" + areaviewer1.HoveringIndex.ToString("X2")); 
        };
        areaviewer1.TileSelectedEvent += delegate(object sender) {
            statusbar1.Push(1,
                    "Selected Tile: 0x" + areaviewer1.SelectedIndex.ToString("X2")); 
        };

        roomeditor1.HoverChangedEvent += delegate() {
            if (roomeditor1.HoveringIndex == -1)
                statusbar1.Push(1,
                        "Selected Tile: 0x" + areaviewer1.SelectedIndex.ToString("X2")); 
            else
                statusbar1.Push(2,
                        "Hovering Tile: (" + roomeditor1.HoveringX +
                        ", " + roomeditor1.HoveringY + ")");
        };

        worldSpinButton.Adjustment = new Adjustment(0, 0, 5, 1, 0, 0);
        dungeonSpinButton.Adjustment = new Adjustment(0, 0, 15, 1, 0, 0);

        pluginCore = new PluginCore(this);

        LoadPlugins();

        if (directory != "")
            OpenProject(directory);
    }

    void LoadPlugins() {
        pluginCore.ReloadPlugins();

        MenuItem pluginMenuItem = null;

        foreach (Widget w in menubar1.AllChildren) {
            if ((w as MenuItem).Name == "PluginsAction") {
                pluginMenuItem = w as MenuItem;
                break;
            }
        }

        var reloadItem = new MenuItem("Reload scripts");
        reloadItem.Activated += (a,b) => LoadPlugins();

        pluginSubMenu = new Menu();
        pluginMenuItem.Submenu = pluginSubMenu;

        pluginSubMenu.Append(reloadItem);

        foreach (Plugin plugin in pluginCore.GetPlugins()) {
             var item = new MenuItem(plugin.Name);
             item.Activated += ((a, b) =>
                     {
                        plugin.Clicked();
//                         try {
//                             plugin.Clicked();
//                         }
//                         catch(Exception e) {
//                             string msg = "The plugin \"" + plugin.Name + "\" threw an exception:\n\n"
//                                     + e.Message;
//                             Gtk.MessageDialog d = new MessageDialog(null,
//                                     DialogFlags.DestroyWithParent,
//                                     MessageType.Error,
//                                     ButtonsType.Ok,
//                                     msg);
//                             d.Run();
//                             d.Destroy();
//                         }
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
        var area = areaviewer1.Area;
        if (area == null)
            return true;
//         IList<byte> changedTiles = area.UpdateAnimations(1);
        return true;
    }

    void OpenProject(string dir) {
        ResponseType response = ResponseType.Yes;
        if (!File.Exists(dir + "/main.s")) {
            Gtk.MessageDialog d = new MessageDialog(this,
                    DialogFlags.DestroyWithParent,
                    MessageType.Warning,
                    ButtonsType.YesNo,
                    "The folder you selected does not have a main.s file. This probably indicates the folder does not contain the ages disassembly. Attempt to continue anyway?");
            response = (ResponseType)d.Run();
            d.Destroy();
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
                d.Destroy();
            }
    */

            musicComboBox.SetConstantsMapping(Project.MusicMapping);
            SetWorld(0);
        }
    }

    void SetArea(Area area) {
        if (Project == null)
            return;
        areaviewer1.SetArea(area);
        areaSpinButton.Value = area.Index;
        roomeditor1.Room.SetArea(area);
        roomeditor1.QueueDraw();
    }

    void SetRoom(Room room) {
        if (Project == null)
            return;
        roomeditor1.SetRoom(room);
        SetArea(room.Area);
        musicComboBox.Active = Project.MusicMapping.IndexOf((byte)room.GetMusicID());
        roomSpinButton.Value = room.Index;

        objectgroupeditor1.SetObjectGroup(room.GetObjectGroup());
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
        Gtk.Dialog d = new Dialog("Save Project?", this,
                DialogFlags.DestroyWithParent,
                Gtk.Stock.Yes, ResponseType.Yes,
                Gtk.Stock.No, ResponseType.No,
                Gtk.Stock.Cancel, ResponseType.Cancel);
        Alignment a = new Alignment(1,0.25f,1,0);
        a.SetSizeRequest(0, 50);
        a.Add(new Gtk.Label(info));
        d.VBox.Add(a);
        d.VBox.ShowAll();
        response = (ResponseType)d.Run();
        d.Destroy();
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
                this,
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
        dialog.Destroy();
    }
    
    protected void OnSaveActionActivated(object sender, EventArgs e)
    {
        if (Project != null)
            Project.Save();
    }

    protected void OnAnimationsActionActivated(object sender, EventArgs e) {
        if ((sender as ToggleAction).Active)
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

    protected void OnNotebook2SwitchPage(object o, SwitchPageArgs args)
    {
        if (Project == null)
            return;
        GLib.Idle.Add(new GLib.IdleHandler(delegate() {
                    Notebook nb = notebook2;
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

    protected void OnAreaSpinButtonValueChanged(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        SpinButton button = sender as SpinButton;
        SetArea(Project.GetIndexedDataType<Area>(button.ValueAsInt));
    }


    protected void OnMusicComboBoxChanged(object sender, EventArgs e) {
        if (Project == null)
            return;
        roomeditor1.Room.SetMusicID(Project.MusicMapping.StringToByte(musicComboBox.ActiveText));
    }

    protected void OnAreaEditorButtonClicked(object sender, EventArgs e)
    {
        if (Project == null)
            return;
        Window win = new Window(WindowType.Toplevel);
        AreaEditor a = new AreaEditor(areaviewer1.Area);
        win.Add(a);
        win.Name = "Edit Area";
        win.ShowAll();
    }

    protected void OnViewObjectsCheckBoxToggled(object sender, EventArgs e)
    {
        roomeditor1.ViewObjects = viewObjectsCheckBox.Active;
        roomeditor1.QueueDraw();
    }

    bool openedWarpEditor = false;
    protected void OnWarpsActionActivated(object sender, EventArgs e) {
        if (openedWarpEditor)
            return;
        WarpEditor editor = new WarpEditor(Project);
        editor.SetMap(roomSpinButton.ValueAsInt >> 8, roomSpinButton.ValueAsInt & 0xff);

        Gtk.Window win = new Window(WindowType.Toplevel);
        win.Modal = false;
        win.Add(editor);

        editor.Destroyed += delegate(object sender2, EventArgs e2) {
            win.Destroy();
        };
        win.Destroyed += delegate(object sender2, EventArgs e2) {
            openedWarpEditor = false;
        };

        openedWarpEditor = true;
        win.ShowAll();
    }

    protected void OnDarkenDungeonRoomsCheckboxToggled(object sender, EventArgs e)
    {
        worldMinimap.DarkenUsedDungeonRooms = darkenDungeonRoomsCheckbox.Active;
    }
}
