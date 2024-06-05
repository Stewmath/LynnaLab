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
        }

        /// Old process will not be closed when calling this, caller must
        /// manage processes on its own
        public bool AttachAndStartProcess(Process p)
        {
            if (process != null)
            {
                process.OutputDataReceived -= AppendText;
                process.ErrorDataReceived -= AppendText;
            }

            this.process = p;
            process.OutputDataReceived += AppendText;
            process.ErrorDataReceived += AppendText;

            if (!process.Start())
                return false;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return true;
        }

        void AppendText(object sender, DataReceivedEventArgs args)
        {
            string text = args.Data;
            if (text == null)
                return;
            // This is called outside the GUI thread, must use Invoke method
            Gtk.Application.Invoke((e, a) =>
            {
                this.Buffer.Text += StripAnsiCodes(text) + '\n';
            });
        }


        static string StripAnsiCodes(string input)
        {
            string pattern = @"\x1B\[[0-9;]*[mK]";
            return Regex.Replace(input, pattern, string.Empty);
        }
    }
}
