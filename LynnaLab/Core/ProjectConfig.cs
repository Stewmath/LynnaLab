using System;
using System.IO;
using YamlDotNet.Serialization;

namespace LynnaLab
{
    public class ProjectConfig {
        public static ProjectConfig Load(string s) {
            var input = new StringReader(s);
			var deserializer = new Deserializer();
            return deserializer.Deserialize<ProjectConfig>(input);
        }


        // Variables imported from YAML config file
        public bool expandedTilesets {get; set;}
    }
}
