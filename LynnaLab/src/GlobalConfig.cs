using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using YamlDotNet.Serialization;

namespace LynnaLab;

/// Class to manage global configuration (stored in the LynnaLab program
/// folder). Distinct from per-project configuration (see ProjectConfig.cs
/// in LynnaLib).
public class GlobalConfig : INotifyPropertyChanged
{
    static readonly string ConfigFile = "global_config.yaml";
    static readonly string ConfigFileComment = @"
# User config file for LynnaLab. You shouldn't need to edit this directly,
# but if you know what you're doing you can edit the commands to build and run the game.

".TrimStart();

    static readonly string DefaultMenuFont = "ZeldaOracles.ttf";
    static readonly string DefaultInfoFont = "RocknRollOne-Regular.ttf";


    GlobalConfig? oldValues;

    public static bool FileExists()
    {
        return File.Exists(ConfigFile);
    }

    public static GlobalConfig? Load()
    {
        if (!FileExists())
            return null;
        var input = System.IO.File.ReadAllText(ConfigFile);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        GlobalConfig? retval = null;
        try
        {
            retval = deserializer.Deserialize<GlobalConfig>(input);
        }
        catch (Exception)
        {
            Modal.DisplayMessageModal("Error", "Error parsing global_config.yaml. Default settings will be used.");
        }

        if (retval != null)
        {
            // Validate values
            if (retval.DisplayScaleFactor < 1.0f)
                retval.DisplayScaleFactor = 1.0f;
            if (!Top.AvailableFonts.Contains(retval.MenuFont))
                retval.MenuFont = DefaultMenuFont;
            if (!Top.AvailableFonts.Contains(retval.InfoFont))
                retval.InfoFont = DefaultInfoFont;

            retval.oldValues = new GlobalConfig(retval);
        }
        return retval;
    }


    public GlobalConfig() { }

    /// Copy constructor: Copy all fields from another instance
    public GlobalConfig(GlobalConfig c)
    {
        var fields = this.GetType().GetFields();
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(PropertyChangedEventHandler))
                continue;
            field.SetValue(this, field.GetValue(c));
        }
    }


    public event PropertyChangedEventHandler? PropertyChanged;

    // Variables imported from YAML config file

    // External programs
    public string? MakeCommand { get; set; } = null;
    public bool UseExternalEmulator { get; set; } = false;
    public string? EmulatorCommand { get; set; } = null;
    public string? EditPngProgram { get; set; } = null;

    // Display
    public bool LightMode { get; set; } = false;
    public Interpolation Interpolation { get; set; } = Interpolation.Bilinear;
    public bool DarkenDuplicateRooms { get; set; } = true;
    public bool ShowBrushPreview { get; set; } = true;
    public bool ShowCoordinateTooltip { get; set; } = false;
    public bool OverrideSystemScaling { get; set; } = false;
    public float DisplayScaleFactor { get; set; } = 1.0f;

    // Fonts
    public string MenuFont { get; set; } = DefaultMenuFont;
    public string InfoFont { get; set; } = DefaultInfoFont;
    public int MenuFontSize { get; set; } = 20;
    public int InfoFontSize { get; set; } = 20;

    // Build & Run dialog
    public bool CloseRunDialogWithEmulator { get; set; } = false;
    public bool CloseEmulatorWithRunDialog { get; set; } = false;

    // Internal emulator
    public float EmulatorVolume
    {
        get;
        set => SetProperty(ref field, value);
    } = 0.5f;

    public bool EmulatorDrawObjectBoxes
    {
        get;
        set => SetProperty(ref field, value);
    } = true;

    public int EmulatorScale
    {
        get;
        set => SetProperty(ref field, value);
    } = 3;

    public KeybindSet<EmulatorKey> EmulatorKeybindings { get; set; } = DefaultEmulatorKeybindings();

    // Other
    public bool AutoAdjustGroupNumber { get; set; } = true;
    public bool ScrollToZoom { get; set; } = true;


    // ================================================================================
    // Public methods
    // ================================================================================

    public void Save()
    {
        if (this.Equals(oldValues))
            return;

        var serializer = new SerializerBuilder()
            .Build();
        var yaml = serializer.Serialize(this);
        System.IO.File.WriteAllText(ConfigFile, ConfigFileComment + yaml);

        oldValues = new GlobalConfig(this);
    }

    public void ResetEmulatorKeybindings()
    {
        EmulatorKeybindings = DefaultEmulatorKeybindings();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = "DEFAULT")
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    static KeybindSet<EmulatorKey> DefaultEmulatorKeybindings()
    {
        KeybindSet<EmulatorKey> ret = new();
        foreach (var (k, v) in new (ImGuiKey, EmulatorKey)[] {
                (ImGuiKey.RightArrow, EmulatorKey.Right),
                (ImGuiKey.LeftArrow, EmulatorKey.Left),
                (ImGuiKey.UpArrow, EmulatorKey.Up),
                (ImGuiKey.DownArrow, EmulatorKey.Down),
                (ImGuiKey.Z, EmulatorKey.A),
                (ImGuiKey.X, EmulatorKey.B),
                (ImGuiKey.RightShift, EmulatorKey.Select),
                (ImGuiKey.Enter, EmulatorKey.Start),
                (ImGuiKey.Space, EmulatorKey.Speedup),

        }) {
            ret.KeyMap[k] = v;
        }
        return ret;
    }
}

public class KeybindSet<T>
{
    // ================================================================================
    // Properties
    // ================================================================================

    [YamlMember]
    public Dictionary<ImGuiKey, T> KeyMap { get; init; } = new();
}
