using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LynnaLab
{
	public class AsmFileParser : FileParser
	{
		public AsmFileParser(Project p, string f)
			: base(p,f)
		{
			string[] lines = File.ReadAllLines(fullFilename);

			for (int i=0; i<lines.Length; i++) {
				string line = lines[i];
				line = line.Split(';')[0].Trim();
				if (line.Length == 0)
					continue;

				// TODO: split tokens more intelligently, ie: recognize this as one token: $8000 | $03
				//string[] tokens = line.Split(new char[] { ' ', '\t'} );
                string[] tokens = Regex.Split(line, @"\s+");
				string warningString = "WARNING while parsing \"" + filename + "\": Line " + (i+1) + ": ";

				string value;

                List<string> standardValues = new List<string>();
                for (int j = 1; j < tokens.Length; j++)
                    standardValues.Add(tokens[j]);

				if (tokens.Length > 0) {
					switch (tokens[0].ToLower()) {
                        // Built-in directives
						case ".define":
							if (tokens.Length < 3) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected .DEFINE to have a string and a value.");
								break;
							}
							value = "";
							for (int j = 2; j < tokens.Length; j++) {
								value += tokens[j];
								value += " ";
							}
							value = value.Trim();
							project.AddDefinition(tokens[1], value);
							break;
						case ".dw":
							if (tokens.Length < 2) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected .DW to have a value.");
								break;
							}
							for (int j=1; j<tokens.Length; j++) {
								string[] values = { tokens[j] };
								Data d = new Data(project, tokens[0].ToLower(), values, 2);
								AddData(d);
							}
							break;
						case ".db":
							if (tokens.Length < 2) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected .DB to have a value.");
								break;
							}
							for (int j=1; j<tokens.Length; j++) {
								string[] values = { tokens[j] };
								Data d = new Data(project, tokens[0].ToLower(), values, 1);
								AddData(d);
							}
							break;

                        // Macros
                        case "m_rgb16":
							if (tokens.Length != 4) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 3 parameters");
								break;
							}
                            {
                                Data d = new RgbData(project, tokens[0].ToLower(), standardValues);
                                AddData(d);
                                break;
                            }
						case "m_gfxheader":
						case "m_gfxheaderforcemode":
							if (tokens.Length < 4 || tokens.Length > 5) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 3-4 parameters");
								break;
							}
							{
								Data d = new GfxHeaderData(project, tokens[0].ToLower(), standardValues);
								AddData(d);
								break;
							}
						case "m_paletteheaderbg":
						case "m_paletteheaderspr":
							if (tokens.Length != 5) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 4 parameters");
								break;
							}
							{
								Data d = new PaletteHeaderData(project, tokens[0].ToLower(), standardValues);
								AddData(d);
								break;
							}
                        case "m_tilesetheader":
							if (tokens.Length != 7) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 6 parameters");
								break;
							}
							{
								Data d = new TilesetHeaderData(project, tokens[0].ToLower(), standardValues);
								AddData(d);
								break;
							}
                        case "m_tilesetdata":
							if (tokens.Length != 2) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 1 parameter");
								break;
							}
							{
                                Stream file = project.GetBinaryFile("tilesets/" + tokens[1] + ".bin");
								Data d = new Data(project, tokens[0].ToLower(), standardValues, (Int32)file.Length);
								AddData(d);
								break;
							}
                        case "m_roomlayoutdata":
							if (tokens.Length != 2) {
								project.WriteLog(warningString);
								project.WriteLogLine("Expected " + tokens[0] + " to take 1 parameter");
								break;
							}
							{
                                AddLabel(new Label(tokens[1], dataList.Count));
								Data d = new Data(project, tokens[0].ToLower(), standardValues, -1);
								AddData(d);
								break;
							}

						default:
							if (tokens[0][tokens[0].Length - 1] == ':') {
								// Label
								string s = tokens[0].Substring(0, tokens[0].Length - 1); 
								Label label = new Label(s,dataList.Count);
								AddLabel(label);
							} else {
								// Unknown data
								project.WriteLog(warningString);
								project.WriteLogLine("Did not understand \"" + tokens[0] + "\".");
							}
							break;
					}
				}
			}

			project.WriteLogLine("Parsed \"" + filename + "\" successfully maybe.");
		}
	}
}
