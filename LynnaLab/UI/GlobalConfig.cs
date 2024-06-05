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
# User config file for LynnaLab. You shouldn't need to edit this directly,
# but if you know what you're doing you can edit the commands to build and run the game.

".TrimStart();


        GlobalConfig oldValues;

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
            var retval = deserializer.Deserialize<GlobalConfig>(input);
            retval.oldValues = new GlobalConfig(retval);
            return retval;
        }


        public GlobalConfig() {}

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
        public string MakeCommand { get; set; }
        public string EmulatorCommand { get; set; }
        public bool CloseRunDialogWithEmulator { get; set; }
    }
}
