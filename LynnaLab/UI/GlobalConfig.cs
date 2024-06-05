using System.IO;
using YamlDotNet.Serialization;

namespace LynnaLab
{
    /// Class to manage global configuration (stored in the LynnaLab program
    /// folder). Distinct from per-project configuration (see ProjectConfig.cs
    /// in LynnaLib).
    public class GlobalConfig
    {
        static readonly string ConfigFile = "config.yaml";
        static readonly string ConfigFileComment = @"
# User config file for LynnaLab. Set the command to run a gameboy emulator here,
# or configure it in LynnaLab.

".TrimStart();

        public static bool Exists()
        {
            return File.Exists(ConfigFile);
        }

        public static GlobalConfig Load()
        {
            var input = System.IO.File.ReadAllText(ConfigFile);
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            return deserializer.Deserialize<GlobalConfig>(input);
        }


        public void Save()
        {
            var serializer = new SerializerBuilder()
                .Build();
            var yaml = serializer.Serialize(this);
            System.IO.File.WriteAllText(ConfigFile, ConfigFileComment + yaml);
        }

        // Variables imported from YAML config file
        public string EmulatorCommand { get; set; }
        public bool CloseRunDialogWithEmulator { get; set; }
    }
}
