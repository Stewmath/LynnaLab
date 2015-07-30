using System;
using System.IO;
using Gtk;
using LynnaLab;

public partial class MainWindow: Gtk.Window
{
    Project Project { get; set; }

    public MainWindow () : base (Gtk.WindowType.Toplevel)
    {
        Build();

        roomeditor1.SetClient(areaviewer1);
        minimap.TileSelectedEvent += delegate(object sender) {
            Room room = minimap.Dungeon.GetRoom(0, minimap.SelectedX, minimap.SelectedY);
            SetRoom(room);
        };

        OpenProject("/home/matthew/programs/gb/ages/ages-disasm");
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

            dungeonSpinButton.Value = 0;
            minimap.Dungeon = Project.GetDataType<Dungeon>(0);
            SetRoom(minimap.Dungeon.GetRoom(0,0,0));
        }
    }

    void SetRoom(Room room) {
        roomeditor1.SetRoom(room);
        areaviewer1.SetArea(room.Area);
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
        minimap.Dungeon = Project.GetDataType<Dungeon>(spinner.ValueAsInt);
        SetRoom(minimap.Dungeon.GetRoom(0, minimap.SelectedX, minimap.SelectedY));
    }
}
