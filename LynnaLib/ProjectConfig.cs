using System.IO;
using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace LynnaLib
{
    /// <summary>
    /// This is the contents of the "config.yaml" file deserialized with YamlDotNet.
    ///
    /// This is also serialized with System.Text.Json during network transmission. Sure, why not.
    /// </summary>
    public class ProjectConfig
    {
        private static readonly log4net.ILog log = LogHelper.GetLogger();

        public static ProjectConfig Load(string basepath)
        {
            string configDirectory = basepath + "/LynnaLab/";
            string filename = configDirectory + "/config.yaml";

            ProjectConfig config;

            try
            {
                var input = new StringReader(File.ReadAllText(filename));
                var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

                config = deserializer.Deserialize<ProjectConfig>(input);
                config.filename = filename;
                return config;
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                log.Warn("Couldn't open config file '" + filename + "'.");
                return null;
            }
        }

        // Variables imported from YAML config file
        [JsonInclude, JsonRequired]
        public string EditingGame { get; private set; }
        [JsonInclude, JsonRequired]
        public bool ExpandedTilesets { get; private set; }

        // Filename of config file. Don't de/serialize this for security reasons.
        [JsonIgnore]
        string filename;


        public void SetEditingGame(string value)
        {
            SetVariable("EditingGame", value);
            EditingGame = value;
        }

        public bool EditingGameIsValid()
        {
            return EditingGame != null && (EditingGame == "ages" || EditingGame == "seasons");
        }

        /// Set a variable to a value and save it immediately. Not using a proper YAML parser for
        /// this because I want to preserve comments. This code is not good but it will work for
        /// this specific use case.
        void SetVariable(string variable, string value)
        {
            string[] lines = File.ReadAllLines(filename);

            for (int i=0; i<lines.Length; i++)
            {
                if (lines[i].Contains(variable + ':'))
                {
                    lines[i] = $"{variable}: {value}";
                    File.WriteAllLines(filename, lines);
                    return;
                }
            }

            throw new ProjectErrorException($"Couldn't find variable \"{variable}\" in project config");
        }
    }
}
