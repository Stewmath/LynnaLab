using System;
using System.Collections.Generic;

namespace LynnaLab
{
/// <summary>
///  This class represents a block of doxygen-like comments in a file. Read-only. Fields are
///  case-insensitive (set to lower case).
/// </summary>
public class DocumentationFileComponent : FileComponent {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    readonly Dictionary<string,string> documentationParams;
    string str;

    // Default field values
    readonly Dictionary<string,string> defaultFields = new Dictionary<string,string>();

    public DocumentationFileComponent(FileParser parser, string str) : base(parser, null) {
        // Setup default fields
        defaultFields["postype"] = "normal";


        this.EndsLine = false;
        this.str = str;

        documentationParams = new Dictionary<string,string>();

        ParseDoc(str);
    }

    public override string GetString() {
        return str;
    }


    /// <summary>
    ///  Returns the string corresponding to a documentation block. Example: if the file
    ///  had "@name{Test}", then GetField("name") would return "Test".
    /// </summary>
    public string GetDocumentationField(string name) {
        name = name.ToLower();
        string output;
        documentationParams.TryGetValue(name, out output);
        if (output == null)
            defaultFields.TryGetValue(name, out output);
        return output;
    }


    /// <summary>
    ///  Takes a block of documentation comments as they appear in the file (ie. with semicolons
    ///  still in) and divides it into key-value pairs.
    ///
    ///  This needs to fix spacing, since newlines should be ignored (unless there are two newlines
    ///  in a row).
    /// </summary>
    void ParseDoc(string str) {
        // Helper function: Trim out the initial comments in a line along with all spaces at start
        // and end.
        Func<string,string> TrimLine = (s) => {
            int i = 0;
            while (i < s.Length && s[i] == ';')
                i++;
            return s.Substring(i).Trim();
        };

        int l = 0;
        string[] lines = str.Split('\n');
        string description = "";
        while (l < lines.Length) {
            string trimmed = TrimLine(lines[l]);
            if (trimmed.Length == 0) {
                description += "\n\n";
                l++;
                continue;
            }
            if (trimmed[0] != '@') {
                // Description is outside of any tags
                description += trimmed + " ";
                l++;
                continue;
            }
            int openPos = trimmed.IndexOf('{');
            string key = trimmed.Substring(1, openPos-1).Trim();

            trimmed = trimmed.Substring(openPos+1).Trim();
            string value = "";
            int closePos = -1;
            while (l < lines.Length-1 && (closePos = trimmed.IndexOf('}')) == -1) {
                value += trimmed + " ";
                trimmed = TrimLine(lines[++l]);
            }

            if (closePos == -1) {
                log.Warn(parser.WarningString + "No closing brace for \"" + key + "\".");
                return;
            }

            value += trimmed.Substring(0,closePos);
            value = value.Trim();
            l++;

            documentationParams.Add(key.ToLower(), value);
        }

        description = description.Trim();
        documentationParams.Add("desc", description);
    }


    /// <summary>
    ///  Used for reading subid lists. See constants/interactionTypes.s for how it's
    ///  formatted...
    ///
    ///  Each element in the list is (k,d), where k is the subid value (or just the key in
    ///  general), and d is the description.
    /// </summary>
    public IList<Tuple<string,string>> GetDocumentationFieldSubdivisions(string name) {
        name = name.ToLower();

        string text = GetDocumentationField(name);
        if (text == null)
            return null;

        var output = new List<Tuple<string,string>>();

        int openBrace;
        while ((openBrace = text.IndexOf('[')) != -1) {
            int closeBrace = openBrace+1;
            int j = 1;
            while (closeBrace<text.Length && j>0) {
                if (text[closeBrace] == '[')
                    j++;
                if (text[closeBrace] == ']')
                    j--;
                closeBrace++;
            }
            closeBrace--;
            if (text[closeBrace] != ']') {
                log.Warn("Missing ']' for \"@" + name + "\"");
                return output;
            }

            int pipe = text.IndexOf('|', openBrace);
            if (pipe == -1 || pipe > closeBrace) {
                log.Warn("Missing ']' for \"@" + name + "\"");
                return output;
            }

            string key = text.Substring(openBrace+1, pipe-(openBrace+1));
            string desc = text.Substring(pipe+1, closeBrace-(pipe+1));
            output.Add(new Tuple<string,string>(key,desc));

            text = text.Substring(closeBrace+1);
        }

        return output;
    }
}
}
