using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LynnaLab
{
    public class FileParser
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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

        // The following variables only used by ParseLine and related functions; they provide
        // context between lines when parsing.

        int ifdefDepth = 0; // The number of nested ifdef statements we're in
        int failedIfdefDepth = 0; // The depth of the last ifdef that failed (if ifdefCondition is false)
        bool ifdefCondition = true; // If false, we're currently skipping over some ifdef code

        string context = "";
        // Values for context:
        // - "RAMSECTION"
        // - "ENUM"

        // Variables for when context == RAMSECTION or ENUM
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


            log.Info("Began parsing \"" + Filename + "\".");

            // Made-up label for the start of the file
            Label l = new Label(this, Basename + "_start");
            l.Fake = true;
            InsertComponentAfter(null, l);


            string[] lines = File.ReadAllLines(FullFilename);

            for (int i=0; i<lines.Length; i++) {
                ParseLine(lines[i], i, fileStructure, fileStructureComments);
            }

            context = "";

            Modified = false;

            log.Info("Finished parsing \"" + Filename + "\".");
        }

        void ParseLine(string pureLine, int i, List<FileComponent> fileStructure, List<string> fileStructureComments) {
            string[] split = pureLine.Split(';');
            string line = split[0];
            string comment = pureLine.Substring(split[0].Length);
            string warningString = "WARNING while parsing \"" + Filename + "\": Line " + (i+1) + ": ";

            // Helper functions

            Action<FileComponent,string> AddComponent = (component, c) => {
                fileStructure.Add(component);
                fileStructureComments.Add(c);
                if (component is Label)
                    AddLabelToDictionaries(component as Label);
            };
            Action<Label> AddLabelAndPopFileStructure = (label) => {
                fileStructure.RemoveAt(fileStructure.Count-1);
                string c = fileStructureComments[fileStructureComments.Count-1];
                fileStructureComments.RemoveAt(fileStructureComments.Count-1);
                AddComponent(label, c);
            };
            Action<Data> AddDataAndPopFileStructure = (data) => {
                fileStructure.RemoveAt(fileStructure.Count-1);
                string c = fileStructureComments[fileStructureComments.Count-1];
                fileStructureComments.RemoveAt(fileStructureComments.Count-1);
                AddComponent(data, c);
            };
            Action PopFileStructure = () => {
                fileStructure.RemoveAt(fileStructure.Count-1);
                fileStructureComments.RemoveAt(fileStructureComments.Count-1);
            };

            // Sub-function: returns true if a meaning for the token was found.
            Func<string[],IList<int>,bool> ParseData = (fTokens,fSpacing) =>
            {
                List<string> standardValues = new List<string>();

                // Variables used for some of the goto's
                int size=-1;

                for (int j = 1; j < fTokens.Length; j++)
                    standardValues.Add(fTokens[j]);

                switch (fTokens[0].ToLower()) {
                    case ".incbin":
                        {
                            Data d = new Data(Project, fTokens[0], standardValues, -1,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                        }
                        break;
                    case ".dw":
                        if (context == "RAMSECTION" || context == "ENUM")
                            break;
                        if (fTokens.Length < 2) {
                            log.Warn(warningString + "Expected .DW to have a value.");
                            break;
                        }
                        size = 2;
                        goto arbitraryLengthData;
                    case ".db":
                        if (context == "RAMSECTION" || context == "ENUM")
                            break;
                        if (fTokens.Length < 2) {
                            log.Warn(warningString + "Expected .DB to have a value.");
                            break;
                        }
                        size = 1;
                        goto arbitraryLengthData;
                    case "dwbe":
                        if (fTokens.Length < 2) {
                            log.Warn(warningString + "Expected dwbe to have a value.");
                            break;
                        }
                        size = 2;
                        goto arbitraryLengthData;
                    case "dbrev":
                        if (fTokens.Length < 2) {
                            log.Warn(warningString + "Expected dbrev to have a value.");
                            break;
                        }
                        size = 1;
                        goto arbitraryLengthData;
arbitraryLengthData:
                        PopFileStructure();
                        for (int j=1; j<fTokens.Length; j++) { // Each value is added as individual data
                            string[] values = { fTokens[j] };
                            List<int> newfSpacing = new List<int> {fSpacing[0],fSpacing[j],0};
                            if (j == fTokens.Length-1)
                                newfSpacing[2] = fSpacing[j+1];

                            Data d = new Data(Project, fTokens[0], values, size,
                                    this, newfSpacing);
                            if (j != fTokens.Length-1)
                                d.EndsLine = false;
                            if (j != 1)
                                d.PrintCommand = false;
                            AddComponent(d, "");
                        }
                        break;
                    case "db":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        address++;
                        break;
                    case "dw":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        address+=2;
                        break;
                    case "dsb":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        address += Project.EvalToInt(fTokens[1]);
                        break;
                    case "dsw":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        address += Project.EvalToInt(fTokens[1])*2;
                        break;

                    case "m_animationloop":
                        {
                            Data d = new Data(Project, fTokens[0], standardValues, 2,
                                this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_rgb16":
                        if (fTokens.Length != 4) {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 3 parameters");
                            break;
                        }
                        {
                            Data d = new RgbData(Project, fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_gfxheader":
                    case "m_gfxheaderforcemode":
                        if (fTokens.Length < 4 || fTokens.Length > 5) {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 3-4 parameters");
                            break;
                        }
                        {
                            Data d = new GfxHeaderData(Project, fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_paletteheaderbg":
                    case "m_paletteheaderspr":
                        if (fTokens.Length != 5) {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 4 parameters");
                            break;
                        }
                        {
                            Data d = new PaletteHeaderData(Project, fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_tilesetheader":
                        if (fTokens.Length != 6) {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 5 parameters");
                            break;
                        }
                        {
                            Data d = new TilesetHeaderData(Project, fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_tilesetdata":
                        if (fTokens.Length != 2) {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 1 parameter");
                            break;
                        }
                        {
                            Stream file = Project.GetBinaryFile("tilesets/" + Project.GameString + "/" + fTokens[1] + ".bin");
                            Data d = new Data(Project, fTokens[0], standardValues,
                                    (Int32)file.Length, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_roomlayoutdata":
                        if (fTokens.Length != 2) {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 1 parameter");
                            break;
                        }
                        {
                            Label l = new Label(this, fTokens[1]);
                            l.Fake = true;
                            AddLabelAndPopFileStructure(l);
                            Data d = new Data(Project, fTokens[0], standardValues, -1,
                                    this, fSpacing);
                            AddComponent(d, "");
                            break;
                        }

                    default:
                        {
                            Data d = null;
                            // Try object commands
                            for (int j=0; j<ObjectGroup.ObjectCommands.Length; j++) {
                                string s = ObjectGroup.ObjectCommands[j];

                                if (s.ToLower() == fTokens[0].ToLower()) {
                                    int minParams = ObjectGroup.ObjectCommandMinParams[j];
                                    int maxParams = ObjectGroup.ObjectCommandMaxParams[j];

                                    if (minParams == -1) minParams = maxParams;
                                    if (maxParams == -1) maxParams = minParams;
                                    if (fTokens.Length-1 < minParams || fTokens.Length-1 > maxParams) {
                                      log.Warn(warningString + "Expected " + fTokens[0] + " to take " +
                                          minParams + "-" + maxParams + "parameter(s)");
                                      break;
                                    }

                                    var objectType = (ObjectType)j;

                                    d = new ObjectData(Project, fTokens[0], standardValues,
                                        this, fSpacing, objectType);
                                    break;
                                }
                            }
                            // Try warp sources
                            foreach (string s in WarpSourceData.WarpCommands) {
                                if (s.ToLower() == fTokens[0].ToLower()) {
                                    d = new WarpSourceData(Project, fTokens[0], standardValues,
                                            this, fSpacing);
                                }
                            }
                            // Try warp dest
                            if (WarpDestData.WarpCommand.ToLower() == fTokens[0].ToLower()) {
                                d = new WarpDestData(Project, fTokens[0], standardValues,
                                        this, fSpacing);
                            }

                            if (d != null) {
                                AddDataAndPopFileStructure(d);
                                break;
                            }
                            return false;
                        }
                }
                return true;
            };

            // Add raw string to file structure, it'll be removed if
            // a better representation is found
            fileStructure.Add(new StringFileComponent(this, line, null));
            fileStructureComments.Add(comment);

            if (line.Trim().Length == 0)
                return;

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


            if (tokens.Length > 0) {

                // Check if we're currently skipping over stuff because of .ifdefs
                if (ifdefCondition == false) {
                    if (tokens[0].ToLower() == ".ifdef") {
                        ifdefDepth++;
                    }
                    else if (tokens[0].ToLower() == ".else" && failedIfdefDepth == ifdefDepth-1) {
                        ifdefCondition = true;
                    }
                    else if (tokens[0].ToLower() == ".endif") {
                        ifdefDepth--;
                        if (ifdefDepth == failedIfdefDepth)
                            ifdefCondition = true;
                    }

                    return;
                }

                switch (tokens[0].ToLower()) {
                    // Built-in directives
                    case ".ramsection": {
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
                                int slot = Project.EvalToInt(slotString);
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

                    case ".enum":
                        context = "ENUM";
                        address = Project.EvalToInt(tokens[1]);
                        break;
                        // Not supported: "DESC" (descreasing order)

                    case ".ende":
                        if (context == "ENUM")
                            context = "";
                        break;

                    case ".define":
                        {
                            if (tokens.Length < 3) {
                                log.Debug(warningString + "Expected .DEFINE to have a string and a value.");
                                break;
                            }
                            string value = "";
                            for (int j = 2; j < tokens.Length; j++) {
                                value += tokens[j];
                                value += " ";
                            }
                            value = value.Trim();
                            AddDefinition(tokens[1], value);
                            break;
                        }

                    case ".ifdef":
                        if (tokens.Length < 2) {
                            log.Warn(warningString + "Expected .IFDEF to have a value.");
                            break;
                        }
                        ifdefDepth++;
                        if (Project.GetDefinition(tokens[1]) != null)
                            ifdefCondition = true;
                        else {
                            ifdefCondition = false;
                            failedIfdefDepth = ifdefDepth-1;
                        }
                        break;

                    case ".else":
                        if (ifdefDepth == 0) {
                            log.Warn(warningString + "Expected .IFDEF before .ENDIF.");
                            break;
                        }
                        ifdefCondition = false;
                        break;

                    case ".endif":
                        if (ifdefDepth == 0) {
                            log.Warn(warningString + "Expected .IFDEF before .ENDIF.");
                            break;
                        }
                        ifdefDepth--;
                        break;

                    default:
                        {
                            bool isData = ParseData(tokens, spacing);

                            // In ramsections or enums, assume any unidentifiable data is a label.
                            // Technically this should be the case in any context, but it's more
                            // useful for the parser to tell me what it doesn't understand.
                            if (!isData && (tokens[0][tokens[0].Length - 1] == ':' || context == "RAMSECTION" || context == "ENUM")) {
                                // Label
                                string s = tokens[0];
                                if (tokens[0][tokens[0].Length-1] == ':')
                                    s = tokens[0].Substring(0, tokens[0].Length - 1); 

                                FileComponent addedComponent;
                                if (context == "RAMSECTION" || context == "ENUM") {
                                    AddDefinition(s, address.ToString());
                                    if (context == "RAMSECTION")
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

                                    // Add raw string to file structure, it'll be removed if a better
                                    // representation is found
                                    fileStructure.Add(new StringFileComponent(
                                                this, line.Substring(tokenStartIndices[1]), spacing2));
                                    fileStructureComments.Add(comment);

                                    for (int j=1; j<tokens.Length; j++)
                                        tokens2[j-1] = tokens[j];
                                    for (int j=1; j<spacing.Count; j++)
                                        spacing2.Add(spacing[j]);
                                    if (!ParseData(tokens2, spacing2)) {
                                        log.Debug(warningString + "Error parsing line.");
                                    }
                                }
                            }
                            else {
                                // Unknown data
                                log.Debug(warningString + "Did not understand \"" + tokens[0] + "\".");
                            }
                        }
                        break;
                }
            }
        }

        void AddDefinition(string def, string value) {
            Project.AddDefinition(def, value);
            definesDictionary[def] = value;
        }

        void AddLabelToDictionaries(Label label) {
            labelDictionary.Add(label.Name, label);
            Project.AddLabel(label.Name, this);
        }

        public Label GetLabel(string labelStr) {
            return labelDictionary[labelStr];
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
                AddLabelToDictionaries(newComponent as Label);
            }

            fileStructure.Insert(i+1, newComponent);
            fileStructureComments.Insert(i+1, comment);
            newComponent.SetFileParser(this);

            Modified = true;

            return true;
        }
        // Insert at the beginning if refComponent is null.
        public bool InsertComponentBefore(FileComponent refComponent, FileComponent newComponent, string comment="") {
            int i;
            if (refComponent == null)
                i = 0;
            else {
                i = fileStructure.IndexOf(refComponent);
                if (i == -1)
                    return false;
            }

            if (newComponent is Label) {
                AddLabelToDictionaries(newComponent as Label);
            }

            fileStructure.Insert(i, newComponent);
            fileStructureComments.Insert(i, comment);
            newComponent.SetFileParser(this);

            Modified = true;

            return true;
        }
        // Parse an array of text (each element is a line) and insert it after
        // refComponent (or at the end if refComponent is null);
        public bool InsertParseableTextAfter(FileComponent refComponent, string[] text) {
            int index;
            if (refComponent == null)
                index = fileStructure.Count-1;
            else {
                index = fileStructure.IndexOf(refComponent);
                if (index == -1)
                    return false;
            }

            context = "";
            List<FileComponent> structure = new List<FileComponent>();
            List<string> structureComments = new List<string>();

            for (int i=0;i<text.Length;i++) {
                ParseLine(text[i], i, structure, structureComments);
            }

            fileStructure.InsertRange(index+1, structure);
            fileStructureComments.InsertRange(index+1, structureComments);

            return true;
        }
        public bool InsertParseableTextBefore(FileComponent refComponent, string[] text) {
            int index;
            if (refComponent == null)
                index = 0;
            else {
                index = fileStructure.IndexOf(refComponent);
                if (index == -1)
                    return false;
            }

            context = "";
            List<FileComponent> structure = new List<FileComponent>();
            List<string> structureComments = new List<string>();

            for (int i=0;i<text.Length;i++) {
                ParseLine(text[i], i, structure, structureComments);
            }

            fileStructure.InsertRange(index, structure);
            fileStructureComments.InsertRange(index, structureComments);

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

            Label l = component as Label;
            if (l != null) {
                labelDictionary.Remove(l.Name);
                Project.RemoveLabel(l.Name);
            }

            Modified = true;
        }

        public void RemoveLabel(string label) {
            Label l;
            if (!labelDictionary.TryGetValue(label, out l))
                return;
            RemoveFileComponent(l);
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
