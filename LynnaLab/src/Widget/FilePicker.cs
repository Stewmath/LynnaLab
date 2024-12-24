using System.IO;
using Num = System.Numerics;

namespace LynnaLab;

// Based on: https://gist.github.com/prime31/91d1582624eb2635395417393018016e
public class FilePicker
{
    static readonly Dictionary<object, FilePicker> _filePickers = new Dictionary<object, FilePicker>();

    public string RootFolder;
    public string CurrentFolder;
    public string SelectedFile;
    public List<string> AllowedExtensions;
    public bool OnlyAllowFolders;
    public bool Closed;

    public static FilePicker GetFolderPicker(object o, string startingPath)
        => GetFilePicker(o, startingPath, null, true);

    public static FilePicker GetFilePicker(object o, string startingPath, string searchFilter = null, bool onlyAllowFolders = false)
    {
        if (File.Exists(startingPath))
        {
            startingPath = new FileInfo(startingPath).DirectoryName;
        }
        else if (string.IsNullOrEmpty(startingPath) || !Directory.Exists(startingPath))
        {
            startingPath = Environment.CurrentDirectory;
            if (string.IsNullOrEmpty(startingPath))
                startingPath = AppContext.BaseDirectory;
        }

        if (!_filePickers.TryGetValue(o, out FilePicker fp))
        {
            fp = new FilePicker();
            fp.RootFolder = startingPath;
            fp.CurrentFolder = startingPath;
            fp.OnlyAllowFolders = onlyAllowFolders;

            if (searchFilter != null)
            {
                if (fp.AllowedExtensions != null)
                    fp.AllowedExtensions.Clear();
                else
                    fp.AllowedExtensions = new List<string>();

                fp.AllowedExtensions.AddRange(searchFilter.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            }

            _filePickers.Add(o, fp);
        }

        return fp;
    }

    public static void RemoveFilePicker(object o) => _filePickers.Remove(o);

    public bool Draw()
    {
        ImGui.Text("Current Folder: " + CurrentFolder);
        bool result = false;

        if (ImGui.BeginChild(1, new Num.Vector2(400, 400)))
        {
            var di = new DirectoryInfo(CurrentFolder);
            if (di.Exists)
            {
                if (di.Parent != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow);
                    if (ImGui.Selectable("(Up Directory)", false, ImGuiSelectableFlags.NoAutoClosePopups))
                        CurrentFolder = di.Parent.FullName;

                    ImGui.PopStyleColor();
                }

                var fileSystemEntries = GetFileSystemEntries(di.FullName);
                foreach (var fse in fileSystemEntries)
                {
                    if (Directory.Exists(fse))
                    {
                        var name = Path.GetFileName(fse);
                        ImGui.PushStyleColor(ImGuiCol.Text, Color.Yellow);
                        if (ImGui.Selectable(name + "/", false, ImGuiSelectableFlags.NoAutoClosePopups))
                            CurrentFolder = fse;
                        ImGui.PopStyleColor();
                    }
                    else
                    {
                        var name = Path.GetFileName(fse);
                        bool isSelected = SelectedFile == fse;
                        if (ImGui.Selectable(name, isSelected, ImGuiSelectableFlags.NoAutoClosePopups))
                            SelectedFile = fse;

                        if (ImGui.IsMouseDoubleClicked(0))
                        {
                            result = true;
                            Close();
                        }
                    }
                }
            }
        }
        ImGui.EndChild();


        if (ImGui.Button("Cancel"))
        {
            result = false;
            Close();
        }

        if (OnlyAllowFolders)
        {
            ImGui.SameLine();
            if (ImGui.Button("Open Current Folder"))
            {
                result = true;
                SelectedFile = CurrentFolder;
                Close();
            }
        }
        else if (SelectedFile != null)
        {
            ImGui.SameLine();
            if (ImGui.Button("Open"))
            {
                result = true;
                Close();
            }
        }

        return result;
    }

    void Close()
    {
        ImGui.CloseCurrentPopup();
        Closed = true;
    }

    bool TryGetFileInfo(string fileName, out FileInfo realFile)
    {
        try
        {
            realFile = new FileInfo(fileName);
            return true;
        }
        catch
        {
            realFile = null;
            return false;
        }
    }

    IEnumerable<string> GetFileSystemEntries(string fullName)
    {
        var files = new SortedSet<string>();
        var dirs = new SortedSet<string>();

        foreach (var fse in Directory.GetFileSystemEntries(fullName, ""))
        {
            if (Directory.Exists(fse))
            {
                dirs.Add(fse);
            }
            else if (!OnlyAllowFolders)
            {
                if (AllowedExtensions != null)
                {
                    var ext = Path.GetExtension(fse);
                    if (AllowedExtensions.Contains(ext))
                        files.Add(fse);
                }
                else
                {
                    files.Add(fse);
                }
            }
        }

        return dirs.Union(files);
    }
}
