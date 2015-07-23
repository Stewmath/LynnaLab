using System;
using System.IO;
using Gtk;
using LynnaLab;

public partial class MainWindow: Gtk.Window
{
	Project project;

	public MainWindow () : base (Gtk.WindowType.Toplevel)
	{
		Build();

		OpenProject("/home/matthew/programs/gb/ages/ages-disasm");

        tilesetviewer1.SetArea(new Area(project, 0));
	}

	protected void OnDeleteEvent (object sender, DeleteEventArgs a)
	{
		project.Close();

		Application.Quit ();
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
			response = ResponseType.Yes;
			if (!File.Exists(basedir + "/main.s")) {
				Gtk.MessageDialog d = new MessageDialog(this,
					                      DialogFlags.DestroyWithParent,
					                      MessageType.Warning,
					                      ButtonsType.YesNo,
					                      "The folder you selected does not have a main.s file. This probably indicates the folder does not contain the ages disassembly. Attempt to continue anyway?");
				response = (ResponseType)d.Run();
				d.Destroy();
			}
			if (response == ResponseType.Yes) {
				OpenProject(basedir);
			}
		}
		dialog.Destroy();
	}

	void OpenProject(string dir) {
		if (project != null) {
			project.Close();
			project = null;
		}
		project = new Project(dir);
		/*
		try {
			project = new Project(dir);
		}
		catch (Exception ex) {
			string outputString = "The following error was encountered while opening the project:\n\n";
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
	}
}
