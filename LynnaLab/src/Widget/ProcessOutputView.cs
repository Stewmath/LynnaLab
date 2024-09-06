using System.Diagnostics;
using System.Text.RegularExpressions;

namespace LynnaLab;

public class ProcessOutputView
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ProcessOutputView()
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    bool jumpToBottom;
    Process process;
    List<TextEntry> textList = new List<TextEntry>();

    // ================================================================================
    // Properties
    // ================================================================================

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render()
    {
        foreach (var entry in textList)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, entry.color);
            ImGui.TextWrapped(entry.text);
            ImGui.PopStyleColor();
        }

        if (jumpToBottom)
        {
            ImGui.SetScrollY(ImGui.GetScrollMaxY());
            jumpToBottom = false;
        }
    }

    public void AppendText(string text, string preset = "system")
    {
        if (text == null)
            return;
        Vector4 color;
        switch (preset)
        {
            case "code":
                color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
                break;
            case "error":
                color = new Vector4(1.0f, 0.0f, 0.0f, 1.0f);
                break;
            case "system":
            default:
                color = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
                break;
        }
        this.textList.Add(new TextEntry { text = StripAnsiCodes(text), color = color });
        jumpToBottom = true;
    }

    /// <summary>
    /// Returns false if the process failed to start.
    /// </summary>
    public bool AttachAndStartProcess(Process process)
    {
        if (process != null)
        {
            process.OutputDataReceived -= AppendTextHandler;
            process.ErrorDataReceived -= AppendTextHandler;
        }

        this.process = process;
        process.OutputDataReceived += AppendTextHandler;
        process.ErrorDataReceived += AppendTextHandler;

        if (!process.Start())
            return false;

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return true;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void AppendTextHandler(object sender, DataReceivedEventArgs args)
    {
        Helper.MainThreadInvoke(() =>
        {
            AppendText(args.Data, "code");
        });
    }

    static string StripAnsiCodes(string input)
    {
        string pattern = @"\x1B\[[0-9;]*[mK]";
        return Regex.Replace(input, pattern, string.Empty);
    }
}

struct TextEntry
{
    public string text;
    public Vector4 color;
}
