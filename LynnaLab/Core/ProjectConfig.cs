using System;
using System.IO;
using YamlDotNet.Serialization;

namespace LynnaLab
{
    public class ProjectConfig {
        public static ProjectConfig Load(string s) {
            var input = new StringReader(s);
			var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            return deserializer.Deserialize<ProjectConfig>(input);
        }


        // Variables imported from YAML config file
        public string EditingGame { get; private set; }
        public bool ExpandedTilesets { get; private set; }
    }
}
