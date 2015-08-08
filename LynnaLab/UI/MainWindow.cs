using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using Gtk;
using LynnaLab;

public partial class MainWindow: Gtk.Window
{
    Project Project { get; set; }

    uint animationTimerID;

    public MainWindow () : base (Gtk.WindowType.Toplevel)
    {
        Build();

        roomeditor1.SetClient(areaviewer1);
        dungeonMinimap.TileSelectedEvent += delegate(object sender) {
            Room room = dungeonMinimap.GetRoom();
            SetRoom(room);
        };
        worldMinimap.TileSelectedEvent += delegate(object sender) {
            Room room = worldMinimap.GetRoom();
            SetRoom(room);
        };

        worldSpinButton.Adjustment = new Adjustment(0, 0, 3, 1, 0, 0);
        dungeonSpinButton.Adjustment = new Adjustment(0, 0, 15, 1, 0, 0);

        OpenProject("/home/matthew/programs/gb/ages/ages-disasm");
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
        IList<byte> changedTiles = area.updateAnimations(1);
        if (changedTiles.Count != 0) {
            foreach (int tile in changedTiles) {
                areaviewer1.DrawTile(tile);
            }
            areaviewer1.QueueDraw();
            roomeditor1.QueueDraw();
        }
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

            dungeonMinimap.Project = Project;
            worldMinimap.Project = Project;
            SetWorld(0);
        }
    }

    void SetArea(Area area) {
        areaviewer1.SetArea(area);
        areaSpinButton.Value = area.Index;
        roomeditor1.Room.SetArea(area);
        roomeditor1.QueueDraw();
    }

    void SetRoom(Room room) {
        roomeditor1.SetRoom(room);
        SetArea(room.Area);
        musicSpinButton.Value = room.GetMusicID();
        roomSpinButton.Value = room.Index;
    }

    void SetDungeon(Dungeon dungeon) {
        dungeonSpinButton.Value = dungeon.Index;
        floorSpinButton.Value = 0;
        floorSpinButton.Adjustment = new Adjustment(0, 0, dungeon.NumFloors-1, 1, 0, 0);
        dungeonMinimap.SetDungeon(dungeon);
        SetRoom(dungeonMinimap.GetRoom());
    }
    void SetDungeon(int index) {
        SetDungeon(Project.GetIndexedDataType<Dungeon>(index));
    }
    void SetWorld(int index) {
        worldSpinButton.Value = index;
        worldMinimap.SetWorld(index);
        SetRoom(worldMinimap.GetRoom());
    }

    void Quit() {
        Project.Close();
        Application.Quit();
    }

    protected void OnDeleteEvent (object sender, DeleteEventArgs a)
    {
        Quit();
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
            string basedir = dialog.Filename;
            OpenProject(basedir);
        }
        dialog.Destroy();
    }
    
    protected void OnSaveActionActivated(object sender, EventArgs e)
    {
        Project.Save();
    }

    protected void OnAnimationsActionActivated(object sender, EventArgs e) {
        if (AnimationsAction.Active)
            StartAnimations();
        else
            EndAnimations();
    }

    protected void OnQuitActionActivated(object sender, EventArgs e)
    {
        ResponseType response;
        Gtk.Dialog d = new Dialog("Exiting", this,
                DialogFlags.DestroyWithParent,
                Gtk.Stock.Yes, ResponseType.Yes,
                Gtk.Stock.No, ResponseType.No,
                Gtk.Stock.Cancel, ResponseType.Cancel);
        Alignment a = new Alignment(1,0.25f,1,0);
        a.SetSizeRequest(0, 50);
        a.Add(new Gtk.Label("Save project before exiting?"));
        d.VBox.Add(a);
        d.VBox.ShowAll();
        response = (ResponseType)d.Run();
        d.Destroy();
        if (response == ResponseType.Yes) {
            Project.Save();
            Quit();
        }
        else if (response == ResponseType.No)
            Quit();
    }

    protected void OnDungeonSpinButtonValueChanged(object sender, EventArgs e)
    {
        SpinButton spinner = sender as SpinButton;
        SetDungeon(Project.GetIndexedDataType<Dungeon>(spinner.ValueAsInt));
    }

    protected void OnFloorSpinButtonValueChanged(object sender, EventArgs e)
    {
        SpinButton spinner = sender as SpinButton;
        dungeonMinimap.Floor = spinner.ValueAsInt;
        SetRoom(dungeonMinimap.Dungeon.GetRoom(dungeonMinimap.Floor, dungeonMinimap.SelectedX, dungeonMinimap.SelectedY));
    }

    protected void OnWorldSpinButtonValueChanged(object sender, EventArgs e)
    {
        SpinButton spinner = sender as SpinButton;
        spinner.Hide();
        SetWorld(spinner.ValueAsInt);
        spinner.Show();
    }

    protected void OnNotebook2SwitchPage(object o, SwitchPageArgs args)
    {
        Notebook nb = o as Notebook;
        if (nb.Page == 0)
            SetWorld(worldSpinButton.ValueAsInt);
        else if (nb.Page == 1)
            SetDungeon(dungeonSpinButton.ValueAsInt);
    }

    protected void OnRoomSpinButtonValueChanged(object sender, EventArgs e)
    {
        SpinButton button = sender as SpinButton;
        SetRoom(Project.GetIndexedDataType<Room>(button.ValueAsInt));
    }

    protected void OnAreaSpinButtonValueChanged(object sender, EventArgs e)
    {
        SpinButton button = sender as SpinButton;
        SetArea(Project.GetIndexedDataType<Area>(button.ValueAsInt));
    }


    protected void OnMusicSpinButtonValueChanged(object sender, EventArgs e)
    {
        SpinButton button = sender as SpinButton;
        roomeditor1.Room.SetMusicID(button.ValueAsInt);
    }

    protected void OnAreaEditorButtonClicked(object sender, EventArgs e)
    {
        Window win = new Window(WindowType.Toplevel);
        AreaEditor a = new AreaEditor(areaviewer1.Area);
        win.Add(a);
        win.Name = "Edit Area";
        win.ShowAll();
    }
}
