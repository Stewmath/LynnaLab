using System;
using System.Collections.Generic;

namespace LynnaLab
{
/// <summary>
///  This class represents a block of doxygen-like comments in a file. Read-only.
/// </summary>
public class DocumentationFileComponent : FileComponent {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    readonly Dictionary<string,string> documentationParams;
    string str;

    public DocumentationFileComponent(FileParser parser, string str) : base(parser, null) {
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
    public string GetField(string name) {
        string output;
        documentationParams.TryGetValue(name, out output);
        return output;
    }


    /// <summary>
    ///  Takes a block of documentation comments as they appear in the file (ie. with semicolons
    ///  still in) and divides it into key-value pairs.
    ///
    ///  This needs to fix spacing, since newlines should be ignored (unless there are two newlines
    ///  in a row? That's a TODO.)
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
            if (trimmed.Length == 0 || trimmed[0] != '@') {
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

            documentationParams.Add(key, value);
        }

        description = description.Trim();
        documentationParams.Add("desc", description);
    }
}
}
