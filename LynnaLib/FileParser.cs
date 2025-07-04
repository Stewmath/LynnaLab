﻿using System.IO;
using System.Text.Json.Serialization;

namespace LynnaLib
{
    struct Command
    {
        public string name;
        public int minParams;
        public int maxParams;
        public int size;

        public Command(string name, int minParams, int maxParams = -1, int size = -1)
        {
            this.name = name;
            this.minParams = minParams;
            if (maxParams == -1)
                this.maxParams = minParams;
            else
                this.maxParams = maxParams;
            this.size = size;
        }
    }

    public class FileParser : TrackedProjectData
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        // List of commands thas should be interpreted as "Data" instances (not all are listed here)
        readonly IReadOnlyList<Command> genericCommandList = new List<Command> {
            new Command("m_chestdata", 3, size: 4),
            new Command("m_continuebithelpersetlast", 0, size: 0),
            new Command("m_continuebithelperunsetlast", 0, size: 0),
            new Command("m_enemysubiddata", 2, size: 2),
            new Command("m_enemysubiddataend", 0, size: 0),
            new Command("m_interactionsubiddata", 3, size: 3),
            new Command("m_interactionsubiddataend", 0, size: 0),
            new Command("m_treasurepointer", 1, size: 4),
            new Command("m_treasuresubid", 5, size: 4),
        };

        /// <summary>
        /// FileParser state fields that are "undoable". Everything outside of here is not affected
        /// by undo/redo.
        /// </summary>
        class State : TransactionState
        {
            [JsonRequired]
            public DictionaryLinkedList<InstanceResolver<FileComponent>> fileStructure = new();
            [JsonRequired]
            public Dictionary<string, InstanceResolver<Label>> labelDictionary = new();

            // Maps a string (key) to a pair (v,d), where v is the value, and d is
            // a DocumentationFileComponent (possible null).
            [JsonRequired]
            public Dictionary<string, Tuple<string, InstanceResolver<DocumentationFileComponent>>> definesDictionary = new();

            [JsonRequired]
            public ulong idCounter;
        }

        // Never track these with state - could be a security problem, allowing clients to have
        // filesystem access
        readonly string filename; // Relative to base directory
        readonly string fullFilename; // Full path

        State state = new State();
        bool _modified;

        // The following variables only used by ParseLine and related functions; they provide
        // context between lines when parsing.

        // This keeps track of doxygen-like comments in the code.
        //
        // This is only for the current "block", which starts when it sees ";;" and ends when
        // a non-comment line is encountered.
        //
        // "context" variable should equal "DOCUMENTATION" while parsing a documentation block.
        //
        // TODO: comments will be lost if this is at the very end of the file...
        string documentationString;

        // Line index (only used for warning strings)
        int currentLine = -1;

        int ifdefDepth = 0; // The number of nested ifdef statements we're in
        int failedIfdefDepth = 0; // The depth of the last ifdef that failed (if ifdefCondition is false)
        bool ifdefCondition = true; // If false, we're currently skipping over some ifdef code

        int unionDepth = 0;

        string context = "";
        // Values for context:
        // - "RAMSECTION"
        // - "ENUM"
        // - "DOCUMENTATION"

        // Variables for when context == RAMSECTION or ENUM
        int bank;
        int address;



        public FileParser(Project p, string f)
            : base(p, f)
        {
            if (!Project.FileExists(f))
                throw new Exception($"File does not exist: {f}");

            this.filename = f;
            this.fullFilename = Project.BaseDirectory + f;

            log.Debug("Began parsing \"" + Filename + "\".");

            string[] lines = Project.GetFileStream(f).ReadAllLines();

            for (int i = 0; i < lines.Length; i++)
            {
                currentLine = i;
                ParseLine(lines[i], FileStructure);
            }

            context = "";

            Modified = false;

            log.Debug("Finished parsing \"" + Filename + "\".");
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private FileParser(Project p, string id, TransactionState s)
            : base(p, id)
        {
            this.state = (State)s;
            this.filename = null;
            this.fullFilename = null;
            this.context = "";

            if (state.fileStructure == null || state.definesDictionary == null || state.labelDictionary == null)
                throw new DeserializationException();
        }


        // Properties

        public DictionaryLinkedList<InstanceResolver<FileComponent>> FileStructure { get { return state.fileStructure; } }
        public Dictionary<string, Tuple<string, InstanceResolver<DocumentationFileComponent>>> DefinesDictionary
        {
            get { return state.definesDictionary; }
        }
        Dictionary<string, InstanceResolver<Label>> LabelDictionary { get { return state.labelDictionary; } }

        public bool Modified
        {
            get { return _modified; }
            set
            {
                _modified = value;
                if (value)
                    Project.MarkModified();
            }
        }
        public string Filename
        {
            get { return filename; }
        }
        public string WarningString
        {
            get
            {
                if (currentLine == -1) // Probably parsing a line given through code, not from the actual file
                    return "While inserting lines into \"" + Filename + "\": ";
                else
                    return $"{Filename}: Line {currentLine}: ";
            }
        }
        protected string FullFilename
        {
            get { return fullFilename; }
        }

        public string Basename
        {
            get
            {
                int i = filename.LastIndexOf('.');
                if (i == -1)
                    return filename;
                return filename.Substring(0, i);
            }
        }


        // Methods

        void ParseLine(string pureLine, DictionaryLinkedList<InstanceResolver<FileComponent>> fileStructure)
        {
            string warningString = WarningString;

            // Helper functions

            // TODO: Is it wise to base IDs on fileparsers when data can technically move between them?
            // TODO: When networking, add a client-specific part to the identifier to reduce chance
            // of collisions between clients?
            Func<string> GenID = () =>
            {
                // Want to avoid collisions by using characters unlikely to be used in filenames
                string id = $"{filename}~$~{state.idCounter}";
                state.idCounter++;
                return id;
            };

            Action<FileComponent> AddComponent = (component) =>
            {
                fileStructure.AddLast(component);
                if (component is Label)
                    AddLabelToDictionaries(component as Label);
            };
            Action PopFileStructure = () =>
            {
                LinkedListNode<InstanceResolver<FileComponent>> last = fileStructure.LastNode;
                Helper.Assert(fileStructure.Remove(last),
                              "Internal error: FileStructure to remove wasn't in list.");
                Project.RemoveDataType<FileComponent>(last.Value.Instance.Identifier);
            };
            Action<FileComponent> AddDataAndPopFileStructure = (data) =>
            {
                PopFileStructure();
                AddComponent(data);
            };

            // Sub-function: returns true if a meaning for the token was found.
            Func<IList<string>, IList<string>, bool> ParseData = (fTokens, fSpacing) =>
            {
                List<string> standardValues = new List<string>();
                string commandLower = fTokens[0].ToLower();

                // Variables used for some of the goto's
                int size = -1;

                for (int j = 1; j < fTokens.Count; j++)
                    standardValues.Add(fTokens[j]);

                switch (commandLower)
                {
                    case ".incbin":
                        {
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, -1,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case ".dw":
                        if (context == "RAMSECTION" || context == "ENUM")
                            break;
                        if (fTokens.Count < 2)
                        {
                            log.Warn(warningString + "Expected .DW to have a value.");
                            break;
                        }
                        size = 2;
                        goto arbitraryLengthData;
                    case ".db":
                        if (context == "RAMSECTION" || context == "ENUM")
                            break;
                        if (fTokens.Count < 2)
                        {
                            log.Warn(warningString + "Expected .DB to have a value.");
                            break;
                        }
                        size = 1;
                        goto arbitraryLengthData;
                    case "dwbe":
                        if (fTokens.Count < 2)
                        {
                            log.Warn(warningString + "Expected dwbe to have a value.");
                            break;
                        }
                        size = 2;
                        goto arbitraryLengthData;
                    case "dbrev":
                        if (fTokens.Count < 2)
                        {
                            log.Warn(warningString + "Expected dbrev to have a value.");
                            break;
                        }
                        size = 1;
                        goto arbitraryLengthData;
                    arbitraryLengthData:
                        PopFileStructure();
                        for (int j = 1; j < fTokens.Count; j++)
                        { // Each value is added as individual data
                            string[] values = { fTokens[j] };
                            List<string> newfSpacing = new List<string> { fSpacing[0], fSpacing[j], "" };
                            if (j == fTokens.Count - 1)
                                newfSpacing[2] = fSpacing[j + 1];

                            Data d = new Data(Project, GenID(), fTokens[0], values, size,
                                    this, newfSpacing);
                            if (j != fTokens.Count - 1)
                                d.EndsLine = false;
                            if (j != 1)
                                d.PrintCommand = false;
                            AddComponent(d);
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
                        address += 2;
                        break;
                    case "dsb":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        address += Project.Eval(fTokens[1]);
                        break;
                    case "dsw":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        address += Project.Eval(fTokens[1]) * 2;
                        break;

                    case "instanceof":
                        if (context != "RAMSECTION" && context != "ENUM")
                            goto default;
                        log.Info(warningString + "instanceof not yet supported.");
                        break;

                    case "m_animationloop":
                        {
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 2,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_rgb16":
                        if (fTokens.Count != 4)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 3 parameters");
                            break;
                        }
                        {
                            Data d = new RgbData(Project, GenID(), fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_gfxheader":
                    case "m_gfxheaderanim":
                    case "m_gfxheaderforcemode":
                        if (fTokens.Count < 3 || fTokens.Count > 5)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 2-4 parameters");
                            break;
                        }
                        {
                            Data d = new GfxHeaderData(Project, GenID(), fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_objectgfxheader":
                        {
                            if (!(fTokens.Count >= 2 && fTokens.Count <= 4))
                            {
                                log.Warn(warningString + "Expected " + fTokens[0] + " to take 1-3 parameters");
                                break;
                            }
                            Data d = new ObjectGfxHeaderData(Project, GenID(), fTokens[0], standardValues, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_gfxheaderstart":
                    case "m_uniquegfxheaderstart":
                        if (fTokens.Count != 3)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 2 parameters");
                        }
                        else
                        {
                            AddDefinitionWhileParsing(fTokens[2], fTokens[1]);

                            // Label is defined in the macro, so we need to handle that manually
                            int index = Project.Eval(fTokens[2]);
                            string labelName;
                            if (commandLower == "m_gfxheaderstart")
                                labelName = "gfxHeader";
                            else
                                labelName = "uniqueGfxHeader";
                            labelName += index.ToString("x2");
                            Label l = new Label(GenID(), this, labelName);
                            AddDataAndPopFileStructure(l);
                        }
                        break;
                    case "m_gfxheaderend":
                        if (!(fTokens.Count >= 1 && fTokens.Count <= 2))
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 0-1 parameters");
                        }
                        else
                        {
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 0, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                        }
                        break;
                    case "m_paletteheaderstart":
                        if (fTokens.Count != 3)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 2 parameters");
                        }
                        else
                        {
                            AddDefinitionWhileParsing(fTokens[2], fTokens[1]);

                            // Label is defined in the macro, so we need to handle that manually
                            int index = Project.Eval(fTokens[2]);
                            string labelName;
                            labelName = "paletteHeader" + index.ToString("x2");
                            Label l = new Label(GenID(), this, labelName);
                            AddDataAndPopFileStructure(l);
                        }
                        break;
                    case "m_paletteheaderend":
                        if (fTokens.Count != 1)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 0 parameters");
                        }
                        else
                        {
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 0, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                        }
                        break;
                    case "m_paletteheaderbg":
                    case "m_paletteheaderspr":
                        if (fTokens.Count != 4)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 3 parameters");
                        }
                        else
                        {
                            Data d = new PaletteHeaderData(Project, GenID(), fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                        }
                        break;
                    case "m_tilesetlayoutheader":
                        if (fTokens.Count != 6)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 5 parameters");
                            break;
                        }
                        {
                            Data d = new TilesetLayoutHeaderData(Project, GenID(), fTokens[0], standardValues,
                                    this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_roomlayoutdata":
                        if (fTokens.Count != 2)
                        {
                            log.Warn(warningString + "Expected " + fTokens[0] + " to take 1 parameter");
                            break;
                        }
                        {
                            Label l = new Label(GenID(), this, fTokens[1]);
                            l.Fake = true;
                            AddDataAndPopFileStructure(l);
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, -1,
                                    this, fSpacing);
                            AddComponent(d);
                            break;
                        }
                    case "m_seasonaltileset":
                        {
                            // In season's "tilesets.s", the m_SeasonalTileset macro points to a label which
                            // contains 4 tileset definitions (one for each season).
                            if (fTokens.Count != 2)
                            {
                                log.Warn(warningString + "Expected " + fTokens[0] + " to take 1 parameter");
                                break;
                            }
                            // Create a data object considered to have a size of 8 bytes
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 8, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_interactiondata":
                        {
                            if (!(fTokens.Count == 2 || fTokens.Count == 4))
                            {
                                log.Warn(warningString + "Expected " + fTokens[0] + " to take 1 or 3 parameters");
                                break;
                            }
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 3, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_enemydata":
                        {
                            if (!(fTokens.Count == 4 || fTokens.Count == 5))
                            {
                                log.Warn(warningString + "Expected " + fTokens[0] + " to take 3-4 parameters");
                                break;
                            }
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 4, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_dungeondata":
                        {
                            if (!(fTokens.Count == 9))
                            {
                                log.Warn(warningString + "Expected " + fTokens[0] + " to take 8 parameters");
                                break;
                            }
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 8, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }
                    case "m_incroomdata":
                        {
                            if (!(fTokens.Count == 2))
                            {
                                log.Warn(warningString + "Expected " + fTokens[0] + " to take 1 parameter");
                                break;
                            }
                            Data d = new Data(Project, GenID(), fTokens[0], standardValues, 8, this, fSpacing);
                            AddDataAndPopFileStructure(d);
                            break;
                        }

                    default:
                        {
                            Data d = null;

                            // Try generic command list
                            foreach (Command command in genericCommandList)
                            {
                                if (command.name.ToLower() == fTokens[0].ToLower())
                                {
                                    if (fTokens.Count - 1 < command.minParams || fTokens.Count - 1 > command.maxParams)
                                    {
                                        log.Warn(warningString + "Expected " + fTokens[0] + " to take " +
                                                command.minParams + "-" + command.maxParams + "parameter(s)");
                                        break;
                                    }

                                    d = new Data(Project, GenID(), fTokens[0], standardValues, command.size, this, fSpacing);
                                    break;
                                }
                            }

                            // Try object commands
                            for (int j = 0; j < ObjectData.ObjectCommands.Length; j++)
                            {
                                string s = ObjectData.ObjectCommands[j];

                                if (s.ToLower() == fTokens[0].ToLower())
                                {
                                    int minParams = ObjectData.ObjectCommandMinParams[j];
                                    int maxParams = ObjectData.ObjectCommandMaxParams[j];

                                    if (minParams == -1) minParams = maxParams;
                                    if (maxParams == -1) maxParams = minParams;
                                    if (fTokens.Count - 1 < minParams || fTokens.Count - 1 > maxParams)
                                    {
                                        log.Warn(warningString + "Expected " + fTokens[0] + " to take " +
                                                minParams + "-" + maxParams + "parameter(s)");
                                        break;
                                    }

                                    int objectDefinitionType = j;

                                    ObjectData lastObjectData = null;
                                    for (var node = fileStructure.LastNode; node != null; node = node.Previous)
                                    {
                                        if (node.Value.Instance is Data)
                                        {
                                            lastObjectData = node.Value.Instance as ObjectData;
                                            break;
                                        }
                                    }
                                    d = new ObjectData(Project, GenID(), fTokens[0], standardValues,
                                            this, fSpacing, objectDefinitionType, lastObjectData);
                                    break;
                                }
                            }
                            // Try warp sources
                            foreach (string s in WarpSourceData.WarpCommands)
                            {
                                if (s.ToLower() == fTokens[0].ToLower())
                                {
                                    d = new WarpSourceData(Project, GenID(), fTokens[0], standardValues,
                                            this, fSpacing);
                                }
                            }
                            // Try warp dest
                            if (WarpDestData.WarpCommand.ToLower() == fTokens[0].ToLower())
                            {
                                d = new WarpDestData(Project, GenID(), fTokens[0], standardValues,
                                        this, fSpacing);
                            }

                            if (d != null)
                            {
                                AddDataAndPopFileStructure(d);
                                break;
                            }
                            return false;
                        }
                }
                return true;
            };

            string pureTrimmedLine = pureLine.Trim();

            // If we're in a documentation block, add any comments to the documentation
            if (context == "DOCUMENTATION")
            {
                if (pureTrimmedLine.Length > 0 && pureTrimmedLine[0] == ';')
                {
                    documentationString += pureLine + '\n';
                    return;
                }
                else
                {
                    context = "";
                    AddComponent(new DocumentationFileComponent(GenID(), this, documentationString));
                    context = "";
                }
            }

            // Check if we're starting a documentation block
            if (pureTrimmedLine.Length >= 2 && pureTrimmedLine.Substring(0, 2) == ";;")
            { // Begin documentation block
                if (context == "DOCUMENTATION")
                    log.Warn(warningString + "Documentation block already open.");
                else
                {
                    context = "DOCUMENTATION";
                    documentationString = pureLine + '\n';
                }
                return;
            }

            string line = pureLine;

            // Add raw string to file structure, it'll be removed if
            // a better representation is found
            AddComponent(new StringFileComponent(GenID(), this, line, null));

            if (line.Trim().Length == 0)
                return;

            // TODO: split tokens more intelligently, ie: recognize this as one token: $8000 | $03
            //string[] tokens = line.Split(new char[] { ' ', '\t'} );
            var tup = Tokenize(line);
            IList<string> tokens = tup.Item1;
            IList<string> spacing = tup.Item2;


            if (tokens.Count > 0)
            {
                // Check if we're currently skipping over stuff because of .ifdefs
                if (ifdefCondition == false)
                {
                    if (tokens[0].ToLower() == ".ifdef" || tokens[0].ToLower() == ".ifndef")
                    {
                        ifdefDepth++;
                    }
                    else if (tokens[0].ToLower() == ".else" && failedIfdefDepth == ifdefDepth - 1)
                    {
                        ifdefCondition = true;
                    }
                    else if (tokens[0].ToLower() == ".endif")
                    {
                        ifdefDepth--;
                        if (ifdefDepth == failedIfdefDepth)
                            ifdefCondition = true;
                    }

                    return;
                }

                // ...Or if we're skipping over stuff for unions (which are not actually supported yet)
                if (tokens[0].ToLower() == ".union")
                {
                    log.Info(warningString + ".union not yet supported");
                    unionDepth++;
                    return;
                }
                // ignoring .nextu for now
                else if (tokens[0].ToLower() == ".endu")
                {
                    unionDepth--;
                    return;
                }

                if (unionDepth > 0)
                    return;

                switch (tokens[0].ToLower())
                {
                    // Built-in directives
                    case ".ramsection":
                        {
                            context = "RAMSECTION";
                            // Find the last token which specifies the name (if wrapped in quotation marks)
                            int tokenIndex = 1;
                            if (tokens[tokenIndex].Contains("\""))
                            {
                                while (tokens[tokenIndex][tokens[tokenIndex].Length - 1] != '"')
                                    tokenIndex++;
                            }
                            tokenIndex++;

                            while (tokenIndex < tokens.Count)
                            {
                                if (tokens[tokenIndex] == "BANK")
                                {
                                    tokenIndex++;
                                    bank = Project.Eval(tokens[tokenIndex++]);
                                }
                                else if (tokens[tokenIndex] == "SLOT")
                                {
                                    tokenIndex++;
                                    string slotString = tokens[tokenIndex++];
                                    int slot = Project.Eval(slotString);
                                    if (slot == 2)
                                        address = 0xc000;
                                    else
                                    { // Assuming slot >= 3
                                        address = 0xd000;
                                    }
                                }
                                else
                                    throw new AssemblyErrorException(warningString
                                            + "Couldn't understand " + tokens[tokenIndex] + ".");
                            }
                            break;
                        }
                    case ".ends":
                        if (context == "RAMSECTION")
                            context = "";
                        break;

                    case ".enum":
                        context = "ENUM";
                        address = Project.Eval(tokens[1]);
                        break;
                    // Not supported: "DESC" (descreasing order)

                    case ".ende":
                        if (context == "ENUM")
                            context = "";
                        break;

                    case ".define":
                        {
                            if (tokens.Count < 3)
                            {
                                log.Debug(warningString + "Expected .DEFINE to have a string and a value.");
                                break;
                            }
                            string value = "";
                            for (int j = 2; j < tokens.Count; j++)
                            {
                                value += tokens[j];
                                value += " ";
                            }
                            value = value.Trim();
                            AddDefinitionWhileParsing(tokens[1], value);
                            break;
                        }

                    case ".ifdef":
                        if (tokens.Count < 2)
                        {
                            log.Warn(warningString + "Expected .IFDEF to have a value.");
                        }
                        else
                        {
                            ifdefDepth++;
                            bool exists = Project.GetDefinition(tokens[1]) != null;
                            if (exists)
                            {
                                ifdefCondition = true;
                            }
                            else
                            {
                                ifdefCondition = false;
                                failedIfdefDepth = ifdefDepth - 1;
                            }
                        }
                        break;

                    case ".ifndef":
                        if (tokens.Count < 2)
                        {
                            log.Warn(warningString + "Expected .IFNDEF to have a value.");
                        }
                        else
                        {
                            ifdefDepth++;
                            bool exists = Project.GetDefinition(tokens[1]) != null;
                            if (!exists)
                            {
                                ifdefCondition = true;
                            }
                            else
                            {
                                ifdefCondition = false;
                                failedIfdefDepth = ifdefDepth - 1;
                            }
                        }
                        break;

                    case ".else":
                        if (ifdefDepth == 0)
                        {
                            log.Warn(warningString + "Expected .IFDEF before .ENDIF.");
                            break;
                        }
                        ifdefCondition = false;
                        break;

                    case ".endif":
                        if (ifdefDepth == 0)
                        {
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
                            if (!isData && (tokens[0][tokens[0].Length - 1] == ':' || context == "RAMSECTION" || context == "ENUM"))
                            {
                                // Label
                                string s = tokens[0];
                                if (tokens[0][tokens[0].Length - 1] == ':')
                                    s = tokens[0].Substring(0, tokens[0].Length - 1);

                                if (s[0] == '@')
                                {
                                    log.Info(warningString + "Ignoring label '" + s + "': child labels not supported.");
                                    break;
                                }

                                FileComponent addedComponent;
                                if (context == "RAMSECTION" || context == "ENUM")
                                {
                                    AddDefinitionWhileParsing(s, address.ToString());
                                    if (context == "RAMSECTION")
                                        AddDefinitionWhileParsing(":" + s, bank.ToString());
                                    PopFileStructure();
                                    StringFileComponent sc = new StringFileComponent(GenID(), this, tokens[0], spacing);
                                    AddComponent(sc);
                                    addedComponent = sc;
                                }
                                else
                                {
                                    Label label = new Label(GenID(), this, s, spacing);
                                    AddDataAndPopFileStructure(label);
                                    addedComponent = label;
                                }
                                if (tokens.Count > 1)
                                { // There may be data directly after the label
                                    string[] tokens2 = new string[tokens.Count - 1];
                                    List<string> spacing2 = new List<string>();

                                    addedComponent.EndsLine = false;

                                    // Add raw string to file structure, it'll be removed if a better
                                    // representation is found
                                    AddComponent(new StringFileComponent(
                                                GenID(), this, line.Substring(spacing[0].Length + tokens[0].Length), spacing2));

                                    for (int j = 1; j < tokens.Count; j++)
                                        tokens2[j - 1] = tokens[j];
                                    for (int j = 1; j < spacing.Count; j++)
                                        spacing2.Add(spacing[j]);
                                    if (!ParseData(tokens2, spacing2))
                                    {
                                        log.Debug(warningString + "Error parsing line.");
                                    }
                                }
                            }
                            else
                            {
                                // Unknown data
                                log.Debug(warningString + "Did not understand \"" + tokens[0] + "\".");
                            }
                            break;
                        }
                }
            }
        }

        /// <summary>
        ///  Adds something to the definesDictionary. Expects that we're adding something on the
        ///  most recent line; it checks the previous line for a DocumentationFileComponent.
        /// </summary>
        void AddDefinitionWhileParsing(string def, string value)
        {
            AddDefinition(def, value);

            DocumentationFileComponent doc = null;
            if (FileStructure.Count >= 2)
                doc = FileStructure.LastNode.Previous.Value.Instance as DocumentationFileComponent;

            // Replace the value from the "AddDefinition" call
            if (doc == null)
                DefinesDictionary[def] = new(value, null);
            else
                DefinesDictionary[def] = new(value, new InstanceResolver<DocumentationFileComponent>(doc));
        }

        void AddDefinition(string def, string value, bool replace = false)
        {
            // Don't add RecordChange() here as it would trigger during loading, not during
            // modifications to data. Changes to the dictionary will still be seen because the data
            // changes that trigger changes to the dictionary will call RecordChange() themselves.
            Project.AddDefinition(def, value, replace);
            DefinesDictionary[def] = new(value, null);
        }

        void AddLabelToDictionaries(Label label)
        {
            // See above note about RecordChange().
            LabelDictionary.Add(label.Name, new(label));
            Project.AddLabel(label.Name, this);
        }

        public Label GetLabel(string labelStr)
        {
            return LabelDictionary[labelStr];
        }

        public Data GetData(string labelStr, int offset = 0)
        {
            Label label = LabelDictionary[labelStr];
            if (label == null)
                throw new InvalidLookupException(string.Format("Label \"{0}\" could not be found.", labelStr));
            return GetData(label, offset);
        }
        public Data GetData(FileComponent component, int offset = 0)
        {
            FileComponent orig = component;
            int origOffset = offset;

            while (component != null && !(component is Data))
                component = component.Next;

            Data data = component as Data;
            while (data != null)
            {
                if (offset == 0)
                    return data;
                if (data.Size == -1)
                    break;
                offset -= data.Size;
                if (offset < 0)
                    break;
                data = data.NextData;
            }
            throw new InvalidLookupException("Provided offset (" + origOffset + ") relative to file component \""
                    + orig.GetString() + "\" was invalid.");
        }

        // Returns the label immediately before this data, or null if there is
        // no label or there is another Data between itself and the label.
        public Label GetDataLabel(Data data)
        {
            FileComponent cmp = data.Prev;

            while (cmp != null)
            {
                if (cmp is Data)
                    return null;
                if (cmp is Label)
                    return cmp as Label;
                cmp = cmp.Prev;
            }
            return null;
        }

        // Attempt to insert newComponent after refComponent, or at the end if
        // refComponent is null.
        public FileComponent InsertComponentAfter(FileComponent refComponent, FileComponent newComponent)
        {
            RecordChange();
            if (refComponent == null)
                FileStructure.AddLast(newComponent);
            else
            {
                if (!FileStructure.Contains(refComponent))
                    throw new Exception("Tried to insert after a FileComponent that's not in the FileParser.");
                FileStructure.AddAfter(refComponent, newComponent);
            }
            newComponent.Attach(this);

            // TODO: this is messy. Would be better if the "fileStructure" was smarter and could
            // handle the labels as we add them, or else just use another function for adding to it.
            if (newComponent is Label)
                AddLabelToDictionaries(newComponent as Label);
            return newComponent;
        }

        // Insert at the beginning if refComponent is null.
        public FileComponent InsertComponentBefore(FileComponent refComponent, FileComponent newComponent)
        {
            RecordChange();
            if (refComponent == null)
                FileStructure.AddFirst(newComponent);
            else
            {
                if (!FileStructure.Contains(refComponent))
                    throw new Exception("Tried to insert before a FileComponent that's not in the FileParser.");
                FileStructure.AddBefore(refComponent, newComponent);
            }
            newComponent.Attach(this);

            if (newComponent is Label)
                AddLabelToDictionaries(newComponent as Label);
            return newComponent;
        }

        // Parse an array of text (each element is a line) and insert it after refComponent (or at
        // the end if refComponent is null). Returns the final inserted FileComponent.
        public FileComponent InsertParseableTextAfter(FileComponent refComponent, string[] text)
        {
            RecordChange();
            context = "";
            var structure = new DictionaryLinkedList<InstanceResolver<FileComponent>>();

            for (int i = 0; i < text.Length; i++)
            {
                currentLine = -1;
                ParseLine(text[i], structure);
            }

            var node = refComponent == null ? null : FileStructure.Find(new(refComponent));
            foreach (InstanceResolver<FileComponent> f in structure)
            {
                var nextNode = new LinkedListNode<InstanceResolver<FileComponent>>(f);
                if (node == null)
                    FileStructure.AddLast(nextNode);
                else
                    FileStructure.AddAfter(node, nextNode);
                node = nextNode;
            }

            return structure.Last;
        }
        public FileComponent InsertParseableTextBefore(FileComponent refComponent, string[] text)
        {
            RecordChange();
            context = "";
            var structure = new DictionaryLinkedList<InstanceResolver<FileComponent>>();

            for (int i = 0; i < text.Length; i++)
            {
                currentLine = -1;
                ParseLine(text[i], structure);
            }

            var node = refComponent == null ? null : FileStructure.Find(new(refComponent));
            foreach (InstanceResolver<FileComponent> f in structure.Reverse())
            {
                var nextNode = new LinkedListNode<InstanceResolver<FileComponent>>(f);
                if (node == null)
                    FileStructure.AddFirst(nextNode);
                else
                    FileStructure.AddBefore(node, nextNode);
                node = nextNode;
            }

            return structure.First;
        }

        public FileComponent GetNextFileComponent(FileComponent reference)
        {
            if (reference == null)
                throw new Exception("Can't get next FileComponent from null reference");
            var node = FileStructure.Find(new(reference));
            return node.Next?.Value.Instance;
        }
        public FileComponent GetPrevFileComponent(FileComponent reference)
        {
            if (reference == null)
                throw new Exception("Can't get prev FileComponent from null reference");
            var node = FileStructure.Find(new(reference));
            return node.Previous?.Value.Instance;
        }

        // Remove a FileComponent (Data, Label) from the fileStructure.
        public void RemoveFileComponent(FileComponent component)
        {
            RecordChange();
            FileStructure.Remove(component);

            Label l = component as Label;
            if (l != null)
            {
                LabelDictionary.Remove(l.Name);
                Project.RemoveLabel(l.Name);
            }
        }

        public void RemoveLabel(string label)
        {
            InstanceResolver<Label> l;
            if (!LabelDictionary.TryGetValue(label, out l))
                return;
            RemoveFileComponent(l);
        }

        // Returns >0 if c2 comes after c1, <0 if c2 comes before c1, or 0 if c1
        // and c2 are the same.
        public int CompareComponentPositions(FileComponent c1, FileComponent c2)
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            if (filename == null)
                throw new Exception("Can't save on remote instance");

            if (!Modified) return;

            List<string> output = new List<string>();
            FileComponent lastComponent = null;
            foreach (var ir in FileStructure)
            {
                FileComponent d = ir.Instance;
                string s = null;

                if (d.Fake)
                    s = null;
                else
                {
                    s = "";
                    if (lastComponent != null && !lastComponent.EndsLine)
                    {
                        s = output[output.Count - 1];
                        output.RemoveAt(output.Count - 1);
                    }
                    d.AboutToSave();
                    s += d.GetString();
                }

                if (s != null)
                    output.Add(s);

                if (d is Data)
                    (d as Data).Modified = false;

                lastComponent = d;
            }
            // NOTE: This is asymmetrical - we're writing directly to the filesystem, unlike loading
            // where we loaded through a MemoryFileStream. Doesn't really matter since saving only
            // works on the server anyway. It does mean the MemoryFileStream will be out of sync
            // with the file itself though - not that that should matter at all.
            // Don't use WriteAllLines as that writes CRLF on windows.
            File.WriteAllText(FullFilename, string.Join('\n', output));

            Modified = false;
        }

        /// <summary>
        ///  Tokenize a string into "tokens" and "spacing" between the tokens.
        ///
        ///  Returns a tuple, where Item1 is the list of tokens, and Item2 is the list of spacing
        ///  between the tokens. There is one more element in the spacing array than the tokens
        ///  array.
        ///
        ///  TODO: though this checks for block comments within a line, it doesn't work when they
        ///  span multiple lines...
        /// </summary>
        public static Tuple<List<string>, List<string>> Tokenize(string line)
        {
            List<string> tokens = new List<string>();
            List<string> spacing = new List<string>();

            Func<string, int, bool> spacingStart = (s, i2) =>
            {
                if (s[i2] == ' ' || s[i2] == ',' || s[i2] == '\t' || s[i2] == ';')
                    return true;
                if (i2 < s.Length - 1 && s.Substring(i2, 2) == "/*")
                    return true;
                return false;
            };

            int i = 0;
            int tokenStartPos = -1;
            int spacingStartPos = -1;
            bool inComment = false; // Inside a /* block comment */

            if (line.Length == 0)
            {
                spacing.Add(" ");
            }
            else
            {
                if (spacingStart(line, i))
                {
                    spacingStartPos = 0;
                    if (line[i] == '/')
                        inComment = true;
                }
                else
                {
                    spacing.Add("");
                    tokenStartPos = 0;
                }
            }

            while (i < line.Length)
            {
                if (line[i] == ';')
                {
                    if (tokenStartPos >= 0)
                    {
                        tokens.Add(line.Substring(tokenStartPos, i - tokenStartPos));
                        tokenStartPos = -1;
                        spacingStartPos = i;
                    }
                    break;
                }
                else if (tokenStartPos >= 0)
                {
                    if (spacingStart(line, i))
                    {
                        tokens.Add(line.Substring(tokenStartPos, i - tokenStartPos));
                        tokenStartPos = -1;
                        if (line[i] == '/')
                        {
                            i++;
                            inComment = true;
                        }
                        spacingStartPos = i++;
                        continue;
                    }
                    else
                    {
                        i++;
                        continue;
                    }
                }
                else if (spacingStartPos >= 0 && inComment)
                {
                    if (i < line.Length - 1 && line.Substring(i, 2) == "*/")
                    {
                        i += 2;
                        inComment = false;
                        continue;
                    }
                    else
                    {
                        i++;
                        continue;
                    }
                }
                else if (spacingStartPos >= 0)
                {
                    if (i < line.Length - 1 && line.Substring(i, 2) == "/*")
                    {
                        inComment = true;
                        i += 2;
                        continue;
                    }
                    else if (!spacingStart(line, i))
                    {
                        spacing.Add(line.Substring(spacingStartPos, i - spacingStartPos));
                        spacingStartPos = -1;
                        tokenStartPos = i++;
                        continue;
                    }
                    else
                    {
                        i++;
                        continue;
                    }
                }
            }

            if (tokenStartPos >= 0)
            {
                tokens.Add(line.Substring(tokenStartPos));
                spacing.Add("");
            }
            else if (spacingStartPos >= 0)
                spacing.Add(line.Substring(spacingStartPos));

            return new Tuple<List<string>, List<string>>(tokens, spacing);
        }


        // This method only works on EXISTING constants in a specific file defined with ".define".
        // No other methods of defining constants will work.
        public void SetDefinition(string constant, string value)
        {
            foreach (StringFileComponent com in FileStructure)
            {
                (var tokens, var spacing) = Tokenize(com.GetString());
                if (tokens.Count >= 2 && tokens[0].ToLower() == ".define" && tokens[1] == constant)
                {
                    com.SetString(tokens[0] + spacing[1] + constant + spacing[2] + value);
                    AddDefinition(constant, value, replace: true);
                    return;
                }
            }
            throw new ProjectErrorException("SetDefinition: Constant \"" + constant + "\" didn't exist already.");
        }

        // Shouldn't really need to do anything in particular here, since the file isn't kept open
        public void Close()
        {
            state.labelDictionary = null;
            state.definesDictionary = null;
            state.fileStructure = null;
        }

        /// <summary>
        /// Always call this BEFORE any change is made that affects the state. It will record the
        /// state before the change is made in order to be able to undo it.
        /// </summary>
        public void RecordChange()
        {
            Project.TransactionManager.CaptureInitialState<State>(this);

            // This is a bit weird. We need to record the project's changes as well, but the fields
            // in the project class that we care about (just defines & labels) are only ever changed
            // due to changes to the FileParser. So this seems to be the best place to record that
            // change.
            // This feels wrong. Something probably needs to be refactored to make the project's
            // state management cleaner.
            Project.CaptureSelfInitialState();

            Modified = true;
        }

        // ================================================================================
        // TrackedProjectData interface functions
        // ================================================================================

        public override TransactionState GetState()
        {
            return state;
        }

        public override void SetState(TransactionState s)
        {
            this.state = (State)s;
            Modified = true;
        }

        public override void InvokeUndoEvents(TransactionState prevState)
        {
        }
    }

    /// <summary>
    /// Helper functions for DictionaryLinkedList to simplify accesses with FileComponents.
    ///
    /// The value type is not "FileComponent" but "InstanceResolver<FileComponent>". Since any two
    /// InstanceResolvers instantiated from the same object are considered equal, they can still be
    /// used for dictionary lookups.
    ///
    /// So this basically just wraps the FileComponent parameters in InstanceResolvers.
    /// </summary>
    public static class DictExtensions
    {
        public static bool Contains(this DictionaryLinkedList<InstanceResolver<FileComponent>> dict, FileComponent value)
        {
            return dict.Contains(new InstanceResolver<FileComponent>(value));
        }

        public static void AddFirst(this DictionaryLinkedList<InstanceResolver<FileComponent>> dict, FileComponent value)
        {
            dict.AddFirst(new InstanceResolver<FileComponent>(value));
        }

        public static void AddLast(this DictionaryLinkedList<InstanceResolver<FileComponent>> dict, FileComponent value)
        {
            dict.AddLast(new InstanceResolver<FileComponent>(value));
        }

        public static void AddBefore(this DictionaryLinkedList<InstanceResolver<FileComponent>> dict, FileComponent node, FileComponent newNode)
        {
            var v1 = new InstanceResolver<FileComponent>(node);
            var v2 = new InstanceResolver<FileComponent>(newNode);
            dict.AddBefore(v1, v2);
        }

        public static void AddAfter(this DictionaryLinkedList<InstanceResolver<FileComponent>> dict, FileComponent node, FileComponent newNode)
        {
            var v1 = new InstanceResolver<FileComponent>(node);
            var v2 = new InstanceResolver<FileComponent>(newNode);
            dict.AddAfter(v1, v2);
        }

        public static void Remove(this DictionaryLinkedList<InstanceResolver<FileComponent>> dict, FileComponent value)
        {
            dict.Remove(new InstanceResolver<FileComponent>(value));
        }
    }
}
