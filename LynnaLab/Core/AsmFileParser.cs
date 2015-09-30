using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LynnaLab
{
    public class AsmFileParser : FileParser
    {
        // Array of lines in the file, will be written back to the file if modified
        string[] lines;
        bool modified;

        public Dictionary<string,string> definesDictionary = new Dictionary<string,string>();


        // I'm a bit evil for using these variables like this, variables only
        // used for the constructor and helper functions
        string context = "";
        // Values for context:
        // - "RAMSECTION"

        int lineIndex;
        // Variables for when context == RAMSECTION
        int bank;
        int address;



        public Dictionary<string,string> DefinesDictionary {
            get { return definesDictionary; }
        }

        public AsmFileParser(Project p, string f)
            : base(p,f)
        {
            Project.WriteLogLine("Began parsing \"" + Filename + "\".");

            lines = File.ReadAllLines(FullFilename);

            for (int i=0; i<lines.Length; i++) {
                lineIndex = i;
                string line = lines[i];
                line = line.Split(';')[0];
                if (line.Trim().Length == 0)
                    continue;

                // TODO: split tokens more intelligently, ie: recognize this as one token: $8000 | $03
                //string[] tokens = line.Split(new char[] { ' ', '\t'} );
                string[] tokens = Regex.Split(line.Trim(), @"\s+");
                int[] tokenStartIndices = new int[tokens.Length];
                int[] tokenEndIndices = new int[tokens.Length];
                {
                    int index = 0;

                    for (int j=0; j<tokens.Length; j++) {
                        while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) {
                            index++;
                        }
                        tokenStartIndices[j] = index;
                        while (index < line.Length && line[index] != ' ' && line[index] != '\t')
                            index++;
                        tokenEndIndices[j] = index;
                    }
                }
                string warningString = "WARNING while parsing \"" + Filename + "\": Line " + (i+1) + ": ";

                string value;

                if (tokens.Length > 0) {
                    switch (tokens[0].ToLower()) {
                        // Built-in directives
                        case ".ramsection":
                            {
                                context = "RAMSECTION";
                                // Find the last token which specifies the name
                                int tokenIndex = 1;
                                while (tokens[tokenIndex][tokens[tokenIndex].Length-1] != '"')
                                    tokenIndex++;
                                tokenIndex++;

                                while (tokenIndex < tokens.Length) {
                                    if (tokens[tokenIndex] == "BANK") {
                                        tokenIndex++;
                                        bank = Project.EvalToInt(tokens[tokenIndex++]);
                                    }
                                    else if (tokens[tokenIndex] == "SLOT") {
                                        tokenIndex++;
                                        string slotString = tokens[tokenIndex++];
                                        int slot = p.EvalToInt(slotString);
                                        if (slot == 2)
                                            address = 0xc000;
                                        else { // Assuming slot >= 3
                                            address = 0xd000;
                                        }
                                    }
                                }
                                break;
                            }
                        case ".ends":
                            if (context == "RAMSECTION")
                                context = "";
                            break;

                        case ".define":
                            if (tokens.Length < 3) {
                                Project.WriteLog(warningString);
                                Project.WriteLogLine("Expected .DEFINE to have a string and a value.");
                                break;
                            }
                            value = "";
                            for (int j = 2; j < tokens.Length; j++) {
                                value += tokens[j];
                                value += " ";
                            }
                            value = value.Trim();
                            AddDefinition(tokens[1], value);
                            break;

                        default:
                            if (tokens[0][tokens[0].Length - 1] == ':') {
                                // Label
                                string s = tokens[0].Substring(0, tokens[0].Length - 1); 
                                if (context == "RAMSECTION") {
                                    AddDefinition(s, address.ToString());
                                    AddDefinition(":"+s, bank.ToString());
                                }
                                else {
                                    Label label = new Label(s,DataList.Count);
                                    AddLabel(label);
                                }
                                if (tokens.Length > 1) {
                                    string[] tokens2 = new string[tokens.Length-1];
                                    int[] tokenStartIndices2 = new int[tokens.Length-1];
                                    int[] tokenEndIndices2 = new int[tokens.Length-1];
                                    for (int j=1; j<tokens.Length; j++) {
                                        tokens2[j-1] = tokens[j];
                                        tokenStartIndices2[j-1] = tokenStartIndices[j];
                                        tokenEndIndices2[j-1] = tokenEndIndices[j];
                                    }
                                    if (!parseData(tokens2, tokenStartIndices2, tokenEndIndices2, warningString)) {
                                        Project.WriteLog(warningString);
                                        Project.WriteLogLine("Error parsing line.");
                                    }
                                }
                            } else {
                                if (!parseData(tokens, tokenStartIndices, tokenEndIndices, warningString)) {
                                    // Unknown data
                                    Project.WriteLog(warningString);
                                    Project.WriteLogLine("Did not understand \"" + tokens[0] + "\".");
                                }
                            }
                            break;
                    }
                }
            }

            Project.WriteLogLine("Parsed \"" + Filename + "\" successfully maybe.");
        }

        // Returns true if a meaning for the token was found.
        bool parseData(string[] tokens, int[] tokenStartIndices, int[] tokenEndIndices, string warningString) {
            List<string> standardValues = new List<string>();

            int minParams=-1,maxParams=-1;
            InteractionType interactionType;

            for (int j = 1; j < tokens.Length; j++)
                standardValues.Add(tokens[j]);

            switch (tokens[0].ToLower()) {
                case ".incbin":
                    {
                        Data d = new Data(Project, tokens[0].ToLower(), standardValues, -1,
                                this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                    }
                    break;
                case ".dw":
                    if (context == "RAMSECTION")
                        break;
                    if (tokens.Length < 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected .DW to have a value.");
                        break;
                    }
                    for (int j=1; j<tokens.Length; j++) {
                        string[] values = { tokens[j] };
                        Data d = new Data(Project, tokens[0].ToLower(), values, 2,
                                this, lineIndex, tokenStartIndices[j], tokenEndIndices[j]);
                        AddData(d);
                    }
                    break;
                case ".db":
                    if (context == "RAMSECTION")
                        break;
                    if (tokens.Length < 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected .DB to have a value.");
                        break;
                    }
                    for (int j=1; j<tokens.Length; j++) {
                        string[] values = { tokens[j] };
                        Data d = new Data(Project, tokens[0].ToLower(), values, 1,
                                this, lineIndex, tokenStartIndices[j], tokenEndIndices[j]);
                        AddData(d);
                    }
                    break;
                case "db":
                    if (context != "RAMSECTION")
                        goto default;
                    address++;
                    break;
                case "dw":
                    if (context != "RAMSECTION")
                        goto default;
                    address+=2;
                    break;
                case "dsb":
                    if (context != "RAMSECTION")
                        goto default;
                    address += Project.EvalToInt(tokens[1]);
                    break;
                case "dsw":
                    if (context != "RAMSECTION")
                        goto default;
                    address += Project.EvalToInt(tokens[1])*2;
                    break;

                case "dwbe":
                    if (tokens.Length < 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected dwbe to have a value.");
                        break;
                    }
                    for (int j=1; j<tokens.Length; j++) {
                        string[] values = { tokens[j] };
                        Data d = new Data(Project, tokens[0].ToLower(), values, 1,
                                this, lineIndex, tokenStartIndices[j], tokenEndIndices[j]);
                        AddData(d);
                    }
                    break;
                case "m_rgb16":
                    if (tokens.Length != 4) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 3 parameters");
                        break;
                    }
                    {
                        Data d = new RgbData(Project, tokens[0].ToLower(), standardValues,
                                this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                        break;
                    }
                case "m_gfxheader":
                case "m_gfxheaderforcemode":
                    if (tokens.Length < 4 || tokens.Length > 5) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 3-4 parameters");
                        break;
                    }
                    {
                        Data d = new GfxHeaderData(Project, tokens[0].ToLower(), standardValues,
                                this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                        break;
                    }
                case "m_paletteheaderbg":
                case "m_paletteheaderspr":
                    if (tokens.Length != 5) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 4 parameters");
                        break;
                    }
                    {
                        Data d = new PaletteHeaderData(Project, tokens[0].ToLower(), standardValues,
                                this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                        break;
                    }
                case "m_tilesetheader":
                    if (tokens.Length != 6) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 5 parameters");
                        break;
                    }
                    {
                        Data d = new TilesetHeaderData(Project, tokens[0].ToLower(), standardValues,
                                this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                        break;
                    }
                case "m_tilesetdata":
                    if (tokens.Length != 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 1 parameter");
                        break;
                    }
                    {
                        Stream file = Project.GetBinaryFile("tilesets/" + tokens[1] + ".bin");
                        Data d = new Data(Project, tokens[0].ToLower(), standardValues,
                                (Int32)file.Length, this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                        break;
                    }
                case "m_roomlayoutdata":
                    if (tokens.Length != 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 1 parameter");
                        break;
                    }
                    {
                        AddLabel(new Label(tokens[1], DataList.Count));
                        Data d = new Data(Project, tokens[0].ToLower(), standardValues, -1,
                                this, lineIndex, tokenStartIndices[1]);
                        AddData(d);
                        break;
                    }

                    // Interactions
                case "interac0":
                    minParams = 1;
                    interactionType = InteractionType.Type0;
                    goto interactionData;
                case "novalue":
                    minParams = 1;
                    interactionType = InteractionType.NoValue;
                    goto interactionData;
                case "doublevalue":
                    minParams = 3;
                    interactionType = InteractionType.DoubleValue;
                    goto interactionData;
                case "pointer":
                    minParams = 1;
                    interactionType = InteractionType.Pointer;
                    goto interactionData;
                case "bosspointer":
                    minParams = 1;
                    interactionType = InteractionType.BossPointer;
                    goto interactionData;
                case "conditional":
                    minParams = 1;
                    interactionType = InteractionType.Conditional;
                    goto interactionData;
                case "randomenemy":
                    minParams = 2;
                    interactionType = InteractionType.RandomEnemy;
                    goto interactionData;
                case "specificenemy":
                    minParams = 3;
                    maxParams = 4;
                    interactionType = InteractionType.SpecificEnemy;
                    goto interactionData;
                case "part":
                    minParams = 2;
                    interactionType = InteractionType.Part;
                    goto interactionData;
                case "quadruplevalue":
                    minParams = 5;
                    interactionType = InteractionType.QuadrupleValue;
                    goto interactionData;
                case "interaca":
                    minParams = 2;
                    maxParams = 3;
                    interactionType = InteractionType.TypeA;
                    goto interactionData;
                case "interacend":
                    minParams = 0;
                    interactionType = InteractionType.End;
                    goto interactionData;
                case "interacendpointer":
                    minParams = 0;
                    interactionType = InteractionType.EndPointer;
                    goto interactionData;


interactionData:
                    {
                        if (minParams == -1) minParams = maxParams;
                        if (maxParams == -1) maxParams = minParams;
                        if (tokens.Length-1 < minParams || tokens.Length-1 > maxParams) {
                            Project.WriteLog(warningString);
                            Project.WriteLogLine("Expected " + tokens[0] + " to take " +
                                    minParams + "-" + maxParams + "parameter(s)");
                            break;
                        }
                        int startColumn=0;
                        if (tokens.Length < 2)
                            startColumn = 0;
                        else
                            startColumn = tokenStartIndices[1];
                        Data d = new InteractionData(Project, tokens[0].ToLower(), standardValues,
                                this, lineIndex, startColumn, interactionType);
                        AddData(d);
                        break;
                    }

                default:
                    return false;
            }
            return true;
        }

        public override string GetLine(int i) {
            return lines[i];
        }
        public override void SetLine(int i, string line) {
            if (lines[i] != line) {
                modified = true;
                lines[i] = line;
            }
        }

        public override void Save() {
            foreach (Data d in DataList) {
                d.Save();
            }
            if (!modified)
                return;
            modified = false;
            File.WriteAllLines(FullFilename, lines);
        }

        void AddDefinition(string def, string value) {
            Project.AddDefinition(def, value);
            definesDictionary[def] = value;
        }
    }
}
