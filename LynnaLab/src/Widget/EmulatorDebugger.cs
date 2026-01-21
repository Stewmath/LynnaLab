#nullable enable

using System.Threading;
using SDL;

namespace LynnaLab;

public class EmulatorDebugger : Frame
{
    public EmulatorDebugger(OraclesEmulator emulator) : base("Debugger")
    {
        this.emu = emulator;
        emu.GB.SetLogCallback((s) => text += s);
        drawer = new EmulatorRenderer(emulator, 4);

        //emu.GB.SetTurboMode(true);
        new Thread(() => { while (true) emu.GB.Run(); }).Start();
    }

    OraclesEmulator emu;
    string text = "";
    string currentLine = "";
    int jumpToBottom = 0;
    EmulatorRenderer drawer;

    public override void Render()
    {
        drawer.RenderAsWindow("Emulator");
        drawer.Active = this.Active;

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
            text += ("> " + currentLine) + '\n';
            emu.GB.DebuggerExecuteCommand(currentLine);
            currentLine = "";
            jumpToBottom = 2;
        }

        if (ImGui.Button("Run"))
        {
            var ticks = emu.GB.Run();
            Console.WriteLine("Run: " + ticks);
        }
        ImGui.SameLine();
        if (ImGui.Button("Run Frame"))
        {
            var ticks = emu.GB.RunFrame();
            Console.WriteLine("Run frames: " + ticks);
        }
    }
}
