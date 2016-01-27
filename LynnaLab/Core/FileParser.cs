using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LynnaLab
{
    public class FileParser
    {
        private Project _project;

        string filename; // Relative to base directory
        string fullFilename; // Full path

        Dictionary<string,Label> labelDictionary = new Dictionary<string,Label>();

        public Dictionary<string,string> definesDictionary = new Dictionary<string,string>();

        // Objects may be raw strings, or Data structures
        // (In the future, maybe have better structures for defines, etc?)
        List<FileComponent> fileStructure = new List<FileComponent>();
        // Parallel array keeping track of comments tacked on to the end of the
        // line
        List<string> fileStructureComments = new List<string>();

        // I'm a bit evil for using these variables like this, variables only
        // used for the constructor and helper functions
        string context = "";
        // Values for context:
        // - "RAMSECTION"

        // Variables for when context == RAMSECTION
        int bank;
        int address;



        public bool Modified {get; set;}
        public Project Project {
            get { return _project; }
        }
        public string Filename {
            get { return filename; }
        }
        protected string FullFilename {
            get { return fullFilename; }
        }

        public string Basename {
            get { 
                int i = filename.LastIndexOf('.');
                if (i == -1)
                    return filename;
                return filename.Substring(0, i);
            }
        }

        public Dictionary<string,string> DefinesDictionary {
            get { return definesDictionary; }
        }

        public FileParser(Project p, string f) {
            _project = p;
            this.filename = f;
            this.fullFilename = Project.BaseDirectory + f;


            Project.WriteLogLine("Began parsing \"" + Filename + "\".");

            // Made-up label for the start of the file
            Label l = new Label(this, Basename + "_start");
            l.Fake = true;
            AddLabel(l);


            string[] lines = File.ReadAllLines(FullFilename);

            for (int i=0; i<lines.Length; i++) {
                string pureLine = lines[i];

                string[] split = pureLine.Split(';');
                string line = split[0];
                string comment = pureLine.Substring(split[0].Length);

                // Add raw string to file structure, it'll be removed if
                // a better representation is found
                fileStructure.Add(new StringFileComponent(this, line, null));
                fileStructureComments.Add(comment);

                if (line.Trim().Length == 0)
                    continue;

                // TODO: split tokens more intelligently, ie: recognize this as one token: $8000 | $03
                //string[] tokens = line.Split(new char[] { ' ', '\t'} );
                string[] tokens = Regex.Split(line.Trim(), @"\s+");

                List<int> spacing = new List<int>();
                int[] tokenStartIndices = new int[tokens.Length];
                {
                    // Generate "spacing" list, keeps track of whitespace
                    // between arguments (+'ve = spaces, -'ve = tabs)
                    int index = 0;

                    for (int j=0; j<tokens.Length+1; j++) {
                        int spaces=0;
                        while (index < line.Length && (line[index] == ' ' || line[index] == '\t')) {
                            if (line[index] == ' ' && spaces >= 0) spaces++;
                            else if (line[index] == '\t' && spaces <= 0) spaces--;
                            index++;
                        }
                        if (j<tokens.Length)
                            tokenStartIndices[j] = index;
                        spacing.Add(spaces);
                        while (index < line.Length && line[index] != ' ' && line[index] != '\t')
                            index++;
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
                                FileComponent addedComponent;
                                if (context == "RAMSECTION") {
                                    AddDefinition(s, address.ToString());
                                    AddDefinition(":"+s, bank.ToString());
                                    PopFileStructure();
                                    StringFileComponent sc = new StringFileComponent(this, tokens[0], spacing);
                                    fileStructure.Add(sc);
                                    fileStructureComments.Add(comment);
                                    addedComponent = sc;
                                }
                                else {
                                    Label label = new Label(this,s,spacing);
                                    AddLabelAndPopFileStructure(label);
                                    addedComponent = label;
                                }
                                if (tokens.Length > 1) { // There may be data directly after the label
                                    string[] tokens2 = new string[tokens.Length-1];
                                    List<int> spacing2 = new List<int>();

                                    addedComponent.EndsLine = false;

                                    // Add raw string to file structure, it'll
                                    // be removed if a better representation is
                                    // found
                                    fileStructure.Add(new StringFileComponent(
                                                this, line.Substring(tokenStartIndices[1]), spacing2));
                                    fileStructureComments.Add(comment);

                                    for (int j=1; j<tokens.Length; j++)
                                        tokens2[j-1] = tokens[j];
                                    for (int j=1; j<spacing.Count; j++)
                                        spacing2.Add(spacing[j]);
                                    if (!parseData(tokens2, spacing2, warningString)) {
                                        Project.WriteLog(warningString);
                                        Project.WriteLogLine("Error parsing line.");
                                    }
                                }
                            } else {
                                if (!parseData(tokens, spacing, warningString)) {
                                    // Unknown data
                                    Project.WriteLog(warningString);
                                    Project.WriteLogLine("Did not understand \"" + tokens[0] + "\".");
                                }
                            }
                            break;
                    }
                }
            }

            Modified = false;

            Project.WriteLogLine("Parsed \"" + Filename + "\" successfully maybe.");
        }

        // Returns true if a meaning for the token was found.
        bool parseData(string[] tokens, IList<int> spacing, string warningString) {
            List<string> standardValues = new List<string>();

            // Variables used for some of the goto's
            int minParams=-1,maxParams=-1;
            InteractionType interactionType;
            int size=-1;

            for (int j = 1; j < tokens.Length; j++)
                standardValues.Add(tokens[j]);

            switch (tokens[0].ToLower()) {
                case ".incbin":
                    {
                        Data d = new Data(Project, tokens[0], standardValues, -1,
                                this, spacing);
                        AddDataAndPopFileStructure(d);
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
                    size = 2;
                    goto arbitraryLengthData;
                case ".db":
                    if (context == "RAMSECTION")
                        break;
                    if (tokens.Length < 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected .DB to have a value.");
                        break;
                    }
                    size = 1;
                    goto arbitraryLengthData;
                case "dwbe":
                    if (tokens.Length < 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected dwbe to have a value.");
                        break;
                    }
                    size = 2;
                    goto arbitraryLengthData;
arbitraryLengthData:
                    PopFileStructure();
                    for (int j=1; j<tokens.Length; j++) { // Each value is added as individual data
                        string[] values = { tokens[j] };
                        List<int> newSpacing = new List<int> {spacing[0],spacing[j],0};
                        if (j == tokens.Length-1)
                            newSpacing[2] = spacing[j+1];

                        Data d = new Data(Project, tokens[0], values, size,
                                this, newSpacing);
                        if (j != tokens.Length-1)
                            d.EndsLine = false;
                        if (j != 1)
                            d.PrintCommand = false;
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

                case "m_rgb16":
                    if (tokens.Length != 4) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 3 parameters");
                        break;
                    }
                    {
                        Data d = new RgbData(Project, tokens[0], standardValues,
                                this, spacing);
                        AddDataAndPopFileStructure(d);
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
                        Data d = new GfxHeaderData(Project, tokens[0], standardValues,
                                this, spacing);
                        AddDataAndPopFileStructure(d);
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
                        Data d = new PaletteHeaderData(Project, tokens[0], standardValues,
                                this, spacing);
                        AddDataAndPopFileStructure(d);
                        break;
                    }
                case "m_tilesetheader":
                    if (tokens.Length != 6) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 5 parameters");
                        break;
                    }
                    {
                        Data d = new TilesetHeaderData(Project, tokens[0], standardValues,
                                this, spacing);
                        AddDataAndPopFileStructure(d);
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
                        Data d = new Data(Project, tokens[0], standardValues,
                                (Int32)file.Length, this, spacing);
                        AddDataAndPopFileStructure(d);
                        break;
                    }
                case "m_roomlayoutdata":
                    if (tokens.Length != 2) {
                        Project.WriteLog(warningString);
                        Project.WriteLogLine("Expected " + tokens[0] + " to take 1 parameter");
                        break;
                    }
                    {
                        Label l = new Label(this, tokens[1]);
                        l.Fake = true;
                        AddLabelAndPopFileStructure(l);
                        Data d = new Data(Project, tokens[0], standardValues, -1,
                                this, spacing);
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
                    interactionType = InteractionType.ItemDrop;
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
                        Data d = new InteractionData(Project, tokens[0], standardValues,
                                this, spacing, interactionType);
                        AddDataAndPopFileStructure(d);
                        break;
                    }

                default:
                    {
                        Data d = null;
                        // Try warp sources
                        foreach (string s in WarpSourceData.WarpCommands) {
                            if (s.ToLower() == tokens[0].ToLower()) {
                                d = new WarpSourceData(Project, tokens[0], standardValues,
                                        this, spacing);
                            }
                        }
                        // Try warp dest
                        if (WarpDestData.WarpCommand.ToLower() == tokens[0].ToLower()) {
                            d = new WarpDestData(Project, tokens[0], standardValues,
                                    this, spacing);
                        }

                        if (d != null) {
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                        return false;
                    }
            }
            return true;
        }

        void AddDefinition(string def, string value) {
            Project.AddDefinition(def, value);
            definesDictionary[def] = value;
        }

        // Appends the given data to the end of the file
        bool AddData(Data data, string comment="") {
            return InsertComponentAfter(null, data, comment);
        }
        // Appends label to the end of the file
        bool AddLabel(Label label, string comment="") {
            return InsertComponentAfter(null, label, comment);
        }

        void AddLabelAndPopFileStructure(Label label) {
            fileStructure.RemoveAt(fileStructure.Count-1);
            string comment = fileStructureComments[fileStructureComments.Count-1];
            fileStructureComments.RemoveAt(fileStructureComments.Count-1);
            this.AddLabel(label, comment);
            Modified = true;
        }
        void AddDataAndPopFileStructure(Data data) {
            fileStructure.RemoveAt(fileStructure.Count-1);
            string comment = fileStructureComments[fileStructureComments.Count-1];
            fileStructureComments.RemoveAt(fileStructureComments.Count-1);
            this.AddData(data, comment);
            Modified = true;
        }
        void PopFileStructure() {
            fileStructure.RemoveAt(fileStructure.Count-1);
            fileStructureComments.RemoveAt(fileStructureComments.Count-1);
            Modified = true;
        }


        public Data GetData(string labelStr, int offset=0) {
            int origOffset = offset;

            Label label = labelDictionary[labelStr];
            if (label != null) {
                FileComponent component = label;
                while (component != null && !(component is Data))
                    component = component.Next;

                Data data = component as Data;
                while (data != null) {
                    if (offset == 0)
                        return data;
                    if (data.Size == -1)
                        break;
                    offset -= data.Size;
                    if (offset < 0)
                        break;
                    data = data.NextData;
                }
            }
            throw new Exception("Provided offset (" + origOffset + ") relative to label \"" + labelStr +
                    "\" was invalid.");
        }

        // Returns the label immediately before this data, or null if there is
        // no label or there is another Data between itself and the label.
        public Label GetDataLabel(Data data) {
            FileComponent cmp = data.Prev;

            while (cmp != null) {
                if (cmp is Data)
                    return null;
                if (cmp is Label)
                    return cmp as Label;
                cmp = cmp.Prev;
            }
            return null;
        }

        // Attempt to insert newComponent after refComponent, or at the end if
        // refComponent is null
        // Returns false if failed (refComponent doesn't exist or newComponent already
        // exists in this file).
        public bool InsertComponentAfter(FileComponent refComponent, FileComponent newComponent, string comment="") {
            int i;
            if (refComponent == null)
                i = fileStructure.Count-1;
            else {
                i = fileStructure.IndexOf(refComponent);
                if (i == -1)
                    return false;
            }

            if (fileStructure.Contains(newComponent))
                return false;

            if (newComponent is Label) {
                Label label = newComponent as Label;
                labelDictionary.Add(label.Name, label);
                Project.AddLabel(label.Name, this);
            }

            fileStructure.Insert(i+1, newComponent);
            fileStructureComments.Insert(i+1, comment);
            newComponent.SetFileParser(this);

            Modified = true;

            return true;
        }
        public bool InsertComponentBefore(FileComponent refComponent, FileComponent newComponent, string comment="") {
            int i = fileStructure.IndexOf(refComponent);
            if (i == -1)
                return false;
            if (fileStructure.Contains(newComponent))
                return false;

            if (newComponent is Label) {
                Label label = newComponent as Label;
                labelDictionary.Add(label.Name, label);
                Project.AddLabel(label.Name, this);
            }

            fileStructure.Insert(i, newComponent);
            fileStructureComments.Insert(i, comment);
            newComponent.SetFileParser(this);

            Modified = true;

            return true;
        }
        public FileComponent GetNextFileComponent(FileComponent reference) {
            int i = fileStructure.IndexOf(reference);
            if (i == -1) return null;
            if (i+1 < fileStructure.Count)
                return fileStructure[i+1];
            return null;
        }
        public FileComponent GetPrevFileComponent(FileComponent reference) {
            int i = fileStructure.IndexOf(reference);
            if (i == -1) return null;
            if (i-1 >= 0)
                return fileStructure[i-1];
            return null;
        }

        // Remove a FileComponent (Data, Label) from the fileStructure.
        public void RemoveFileComponent(FileComponent component) {
            int index = fileStructure.IndexOf(component);
            if (index == -1) return;

            fileStructure.RemoveAt(index);
            fileStructureComments.RemoveAt(index);

            Modified = true;
        }

        // Returns >0 if c2 comes after c1, <0 if c2 comes before c1, or 0 if c1
        // and c2 are the same.
        public int CompareComponentPositions(FileComponent c1, FileComponent c2) {
            if (c1 == c2)
                return 0;
            int p1 = fileStructure.IndexOf(c1);
            int p2 = fileStructure.IndexOf(c2);
            if (p1 == -1 || p2 == -1)
                throw new NotFoundException();
            return p2.CompareTo(p1);
        }

        public void Save() {
            if (!Modified) return;

            List<string> output = new List<string>();
            FileComponent lastComponent = null;
            for (int i=0;i<fileStructure.Count;i++) {
                string s = null;

                FileComponent d = fileStructure[i];
                if (d.Fake)
                    s = null;
                else {
                    s = "";
                    if (lastComponent != null && !lastComponent.EndsLine) {
                        s = output[output.Count-1];
                        output.RemoveAt(output.Count-1);
                    }
                    s += d.GetString();
                    if (d.EndsLine)
                        s += fileStructureComments[i];
                }

                if (s != null)
                    output.Add(s);

                if (d is Data)
                    (d as Data).Modified = false;

                lastComponent = d;
            }
            File.WriteAllLines(FullFilename, output);

            Modified = false;
        }
    }
}
