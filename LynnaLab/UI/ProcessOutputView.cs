using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace LynnaLab
{
    public class ProcessOutputView : Gtk.TextView
    {
        Process process;

        public ProcessOutputView() : base()
        {
            this.Editable = false;
            this.CanFocus = false;

            // Tags for text coloring
            var tagTable = Buffer.TagTable;
            var redTag = new Gtk.TextTag("red");
            var greenTag = new Gtk.TextTag("green");
            redTag.Foreground = "red";
            greenTag.Foreground = "green";
            tagTable.Add(redTag);
            tagTable.Add(greenTag);
        }

        /// Old process will not be closed when calling this, caller must manage
        /// processes on its own
        public bool AttachAndStartProcess(Process p)
        {
            if (process != null)
            {
                process.OutputDataReceived -= AppendTextHandler;
                process.ErrorDataReceived -= AppendTextHandler;
            }

            this.process = p;
            process.OutputDataReceived += AppendTextHandler;
            process.ErrorDataReceived += AppendTextHandler;

            if (!process.Start())
                return false;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return true;
        }

        public void AppendText(string text, string tag = null)
        {
            if (text == null)
                return;
            text = StripAnsiCodes(text) + "\n";

            var iter = Buffer.EndIter;
            Buffer.Insert(ref iter, text);

            if (tag != null)
            {
                var startIter = Buffer.EndIter;
                var endIter = Buffer.EndIter;
                endIter.BackwardChars(text.Length);
                Buffer.ApplyTag(tag, startIter, endIter);
            }
        }

        void AppendTextHandler(object sender, DataReceivedEventArgs args)
        {
            // This is called outside the GUI thread, must use Invoke method
            Gtk.Application.Invoke((e, a) =>
            {
                AppendText(args.Data);
            });
        }

        static string StripAnsiCodes(string input)
        {
            string pattern = @"\x1B\[[0-9;]*[mK]";
            return Regex.Replace(input, pattern, string.Empty);
        }
    }
}
