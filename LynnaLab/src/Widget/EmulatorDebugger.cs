using System.Threading;
using Emulation;

namespace LynnaLab;

public class EmulatorDebugger : Frame, IDisposable
{
    public EmulatorDebugger(EmulatorManager manager) : base("Debugger")
    {
        this.emu = manager.Emulator;
        this.manager = manager;

        text = """
This is the SameBoy debugger console.

Examples to get you started:

- See Link's health:
    > p [wLinkHealth]

- Watchpoint triggering when Link gains or loses health:
    > watch wLinkHealth

- Breakpoint that triggers on textboxes:
    > break showText

- Resume execution after a watchpoint or breakpoint triggers:
    > continue

- Get general help, or help on a specific command:
    > help
    > help watch

""";
        emu.GB.SetLogCallback((s) =>
        {
            text += s;
        });

        emu.GB.SetInputCallback(SyncInputCallback);
        emu.GB.SetAsyncInputCallback(AsyncInputCallback);
    }

    OraclesEmulator emu;
    EmulatorManager manager;
    string text = "";
    string currentLine = "";
    string lastLine = "";
    string? inputText = null;
    int jumpToBottom = 0;
    bool inputReady = false;
    readonly object inputLock = new();

    public override void Render()
    {
        ImGui.PushFont(Top.MonoFont, Top.GlobalConfig.MenuFontSize);

        var size = new Vector2(0.0f, ImGui.GetWindowSize().Y * 0.8f);
        ImGui.BeginChild("CHILD", size, 0, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        foreach (string l in text.Split('\n'))
        {
            ImGui.TextWrapped(l);
        }

        if (jumpToBottom != 0)
        {
            ImGui.SetScrollY(ImGui.GetScrollMaxY());
            jumpToBottom--;
        }

        ImGui.EndChild();

        if (jumpToBottom == 1)
            ImGui.SetKeyboardFocusHere();

        if (ImGui.InputText("##Text input", ref currentLine, Annotation.MAX_TEXT_LENGTH, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            if (currentLine.Trim().Length == 0)
                currentLine = lastLine;

            text += '\n' + ("> " + currentLine) + '\n';
            lock (inputLock)
            {
                inputText += currentLine;
                inputReady = true;
                Monitor.PulseAll(inputLock);
            }
            lastLine = currentLine;
            currentLine = "";
            jumpToBottom = 4;
        }

        /*
        if (!paused)
            ImGui.BeginDisabled();
        if (ImGui.Button("Run Frame"))
        {
            var ticks = emu.GB.RunFrame();
            Console.WriteLine("Run frames: " + ticks);
        }
        if (!paused)
            ImGui.EndDisabled();
        */

        ImGui.PopFont();
    }

    public void Dispose()
    {
        emu = null!;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Input callback while emulator is paused
    /// </summary>
    string? SyncInputCallback()
    {
        string? retval;
        lock (inputLock)
        {
            while (!inputReady)
                Monitor.Wait(inputLock);

            Debug.Assert(inputText != null);
            retval = inputText;
            inputText = null;
            inputReady = false;
        }
        return retval;
    }

    /// <summary>
    /// Input callback while emulator is running
    /// </summary>
    string? AsyncInputCallback()
    {
        if (inputText == null)
            return null;

        string? retval;
        lock (inputLock)
        {
            retval = inputText;
            inputText = null;
            inputReady = false;
        }

        return retval;
    }
}
