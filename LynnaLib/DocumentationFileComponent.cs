using System;
using System.Collections.Generic;

namespace LynnaLib
{
    /// <summary>
    ///  This class represents a block of doxygen-like comments in a file. Read-only. Fields are
    ///  case-insensitive (set to lower case).
    /// </summary>
    public class DocumentationFileComponent : FileComponent
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        readonly Dictionary<string, string> documentationParams;
        readonly string origString;

        readonly List<string> _keys; // Maintained separately from documentationParams to preserve original case


        // Default field values
        readonly Dictionary<string, string> defaultFields = new Dictionary<string, string>();


        public ICollection<string> Keys
        {
            get { return _keys; }
        }

        public DocumentationFileComponent(FileParser parser, string str)
            : base(parser, null, () => new FileComponentState())
        {
            // Setup default fields
            defaultFields["postype"] = "normal";

            _keys = new List<string>();

            this.EndsLine = false;
            this.origString = str;

            documentationParams = ParseDoc(str, _keys);
        }

        public override string GetString()
        {
            return origString;
        }


        /// <summary>
        ///  Returns the string corresponding to a documentation block. Example: if the file
        ///  had "@name{Test}", then GetField("name") would return "Test".
        /// </summary>
        public string GetField(string name)
        {
            name = name.ToLower();
            string output;
            documentationParams.TryGetValue(name, out output);
            if (output == null)
                defaultFields.TryGetValue(name, out output);
            return output;
        }



        /// <summary>
        ///  Static method; Takes a block of documentation comments as they appear in the file (ie. with
        ///  semicolons possibly still in) and divides it into key-value pairs.
        ///
        ///  This needs to fix spacing, since newlines should be ignored (unless there are two newlines
        ///  in a row).
        ///
        ///  This is also used when parsing a "second layer", ie. parsing the inside of a field with
        ///  Documentation.GetSubDocumetation().
        ///
        ///  The dictionary it returns has all its keys set to lowercase. That's why it takes a "keys"
        ///  list to return the original keys with their case intact.
        ///
        ///  TODO: use StringBuilder
        /// </summary>
        public static Dictionary<string, string> ParseDoc(string str, List<string> keys = null)
        {
            var fields = new Dictionary<string, string>();

            // Helper function: Trim out the initial comments in a line along with all spaces at start
            // and end.
            Func<string, string> TrimLine = (s) =>
            {
                int i = 0;
                while (i < s.Length && s[i] == ';')
                    i++;
                return s.Substring(i).Trim();
            };

            // Trim all lines of semicolons and spaces, concatenate lines when necessary, before
            // continuing
            string[] lines = str.Split('\n');
            string text = "";

            for (int l = 0; l < lines.Length; l++)
            {
                string line = TrimLine(lines[l]);
                if (line == "")
                    text += "\n\n";
                else
                    text += " " + line;
            }
            text = text.Trim();

            // Add newlines where explicitly requested
            text = text.Replace("\\n", "\n");


            // Now parse the text
            string description = "";
            int j = 0;
            while (j < text.Length)
            {
                int nextAt = text.IndexOf('@');
                if (nextAt == -1)
                {
                    break;
                }

                int openPos = text.IndexOf('{', nextAt);
                if (openPos == -1)
                    break;

                int closePos = openPos + 1;
                int depth = 1;
                while (closePos < text.Length)
                {
                    if (text[closePos] == '{')
                        depth++;
                    if (text[closePos] == '}')
                    {
                        depth--;
                        if (depth == 0)
                            break;
                    }
                    closePos++;
                }

                if (closePos >= text.Length)
                    break;

                string field = text.Substring(nextAt + 1, openPos - (nextAt + 1)).Trim();
                string value = text.Substring(openPos + 1, closePos - (openPos + 1)).Trim();
                fields[field.ToLower()] = value;
                if (keys != null)
                    keys.Add(field);

                string addToDesc = text.Substring(0, nextAt);
                if (addToDesc.Trim() != "")
                    description += addToDesc;
                text = text.Substring(closePos + 1);
            }
            // On end, text should contain anything remaining to be added to description

            description += text;

            description = description.Trim();
            fields.Add("desc", description);
            keys.Add("desc");

            return fields;
        }
    }
}
