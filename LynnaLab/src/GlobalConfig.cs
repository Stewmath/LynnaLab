using System.IO;
using YamlDotNet.Serialization;

namespace LynnaLab;

/// Class to manage global configuration (stored in the LynnaLab program
/// folder). Distinct from per-project configuration (see ProjectConfig.cs
/// in LynnaLib).
public class GlobalConfig
{
    static readonly string ConfigFile = "global_config.yaml";
    static readonly string ConfigFileComment = @"
# User config file for LynnaLab. You shouldn't need to edit this directly,
# but if you know what you're doing you can edit the commands to build and run the game.

".TrimStart();

    static readonly string DefaultMenuFont = "ZeldaOracles.ttf";
    static readonly string DefaultInfoFont = "RocknRollOne-Regular.ttf";


    GlobalConfig oldValues;

    public static bool FileExists()
    {
        return File.Exists(ConfigFile);
    }

    public static GlobalConfig Load()
    {
        if (!FileExists())
            return null;
        var input = System.IO.File.ReadAllText(ConfigFile);
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();

        GlobalConfig retval = null;
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
            field.SetValue(this, field.GetValue(c));
        }
    }

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

    // Variables imported from YAML config file

    // Advanced settings
    public string MakeCommand { get; set; }
    public string EmulatorCommand { get; set; }

    // Display
    public bool LightMode { get; set; } = false;
    public Interpolation Interpolation { get; set; } = Interpolation.Bilinear;
    public bool DarkenDuplicateRooms { get; set; } = true;
    public bool ShowBrushPreview { get; set; } = true;
    public bool OverrideSystemScaling { get; set; } = false;
    public float DisplayScaleFactor { get; set; } = 1.0f;

    // Fonts
    public string MenuFont { get; set; } = DefaultMenuFont;
    public string InfoFont { get; set; } = DefaultInfoFont;
    public int MenuFontSize { get; set; } = 18;
    public int InfoFontSize { get; set; } = 20;

    // Build & Run dialog
    public bool CloseRunDialogWithEmulator { get; set; } = false;
    public bool CloseEmulatorWithRunDialog { get; set; } = false;

    // Other
    public bool AutoAdjustGroupNumber { get; set; } = true;
    public bool ScrollToZoom { get; set; } = true;
}
