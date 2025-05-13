using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LynnaLab;

public class BuildDialog : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public BuildDialog(ProjectWorkspace workspace, string name)
        : base(name)
    {
        this.Workspace = workspace;
        base.DefaultSize = new Vector2(500, 600);

        base.ClosedEvent += (_, _) => Close();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    ProcessOutputView processView;

    Process makeProcess;
    Process emulatorProcess;

    bool makeLaunchFailed;

    float bottomPanelSize = 0.0f;


    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    // Private properties
    GlobalConfig GlobalConfig { get { return Top.GlobalConfig; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.PushFont(Top.InfoFont);
        ImGui.TextWrapped("Building oracles-disasm. The first time will take several minutes.");
        ImGui.PopFont();

        if (ImGui.BeginChild("Build Dialog ProcessView", new Vector2(0.0f, -bottomPanelSize), ImGuiChildFlags.FrameStyle))
        {
            processView?.Render();
        }
        ImGui.EndChild();

        float startY = ImGui.GetCursorScreenPos().Y;

        ImGuiX.Checkbox("Closing emulator closes dialog",
                        new Accessor<bool>(() => GlobalConfig.CloseRunDialogWithEmulator));
        ImGuiX.Checkbox("Closing dialog closes emulator",
                        new Accessor<bool>(() => GlobalConfig.CloseEmulatorWithRunDialog));

        bottomPanelSize = ImGui.GetCursorScreenPos().Y - startY;
    }

    /// <summary>
    /// Halt compilation
    /// </summary>
    public void HaltMake()
    {
        if (makeProcess != null)
        {
            makeProcess.Exited -= OnMakeExited;
            makeProcess.Kill(true);
            makeProcess = null;
        }
    }

    /// <summary>
    /// Halt emulator
    /// </summary>
    public void HaltEmulator()
    {
        if (emulatorProcess != null)
        {
            emulatorProcess.Exited -= OnEmulatorExited;
            emulatorProcess.Kill(true);
            emulatorProcess = null;
        }
    }

    /// <summary>
    /// Close dialog + halt compilation/execution
    /// </summary>
    public void Close()
    {
        HaltMake();
        if (GlobalConfig.CloseEmulatorWithRunDialog)
            HaltEmulator();
        Active = false;

        // Just in case checkboxes were modified
        GlobalConfig.Save();
    }

    public void BeginCompile()
    {
        // Stop previous compilation if it's in progress
        HaltMake();

        Active = true;

        string makeCommand = GlobalConfig.MakeCommand;

        if (makeCommand == null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                makeCommand = "C:/msys64/msys2_shell.cmd | -here -no-start -defterm -ucrt64 -shell bash -c \"make {GAME}\"";
            }
            else
            {
                makeCommand = "/usr/bin/make | {GAME}";
            }

            GlobalConfig.MakeCommand = makeCommand;
        }

        var (fileName, arguments) = SubstituteString(makeCommand);

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = Project.BaseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // When using quickstart, the environment variable EXTRA_DEFINES is
        // passed to the assembler by the makefile to set the position
        if (Workspace.QuickstartData.Enabled)
        {
            string definitions = "";
            var q = Workspace.QuickstartData;
            var definitionList = new Dictionary<string, byte>
                {
                    { "QUICKSTART_ENABLE", 1 },
                    { "QUICKSTART_GROUP", q.group },
                    { "QUICKSTART_ROOM", q.room },
                    { "QUICKSTART_SEASON", q.season },
                    { "QUICKSTART_Y", q.y },
                    { "QUICKSTART_X", q.x },
                };

            foreach (var (f, v) in definitionList)
            {
                definitions += $"-D {f}={v} ";
            }

            startInfo.EnvironmentVariables["ORACLE_EXTRA_DEFINES"] = definitions;
        }

        // Force the assembler to run each time, mainly to ensure the
        // quickstart defines get updated
        startInfo.EnvironmentVariables["ORACLE_FORCE_REBUILD"] = "1";

        makeProcess = new Process { StartInfo = startInfo };
        makeProcess.EnableRaisingEvents = true;
        makeProcess.Exited += OnMakeExited;

        processView = new ProcessOutputView();

        // Attempt to build disassembly
        processView.AppendText("Building with command:");
        processView.AppendText($"\"{fileName}\" {arguments}", "code");
        try
        {
            makeLaunchFailed = !processView.AttachAndStartProcess(makeProcess);
        }
        catch (Win32Exception)
        {
            makeLaunchFailed = true;
        }

        if (makeLaunchFailed)
        {
            processView.AppendText("Failed to launch make process with command:", "error");
            processView.AppendText(makeCommand);
            processView.AppendText("\nIf you're on Windows, MSYS2 must be installed.");
            return;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void OnMakeExited(object sender, object args)
    {
        Helper.MainThreadInvoke(OnMakeExitedBody);
    }

    void OnMakeExitedBody()
    {
        if (!makeProcess.HasExited)
        {
            processView.AppendText("Internal error: Called OnMakeExited without process exiting?");
            return;
        }

        if (makeProcess.ExitCode != 0)
        {
            processView.AppendText($"\nError: make exited with code {makeProcess.ExitCode}", "error");
            return;
        }

        processView.AppendText("\nBuild completed successfully!\n", "success");

        if (GlobalConfig.EmulatorCommand != null)
        {
            RunGame(GlobalConfig.EmulatorCommand);
            return;
        }

        // Open a modal telling the user to select an emulator, then open the file chooser dialog
        Modal.OpenModal(
            "Select Emulator",
            () =>
            {
                ImGui.Text($"Your gameboy emulator path has not been configured.\nSelect your emulator executable file now to run {Project.GameString}.gbc.");
                if (ImGui.Button("OK"))
                {
                    SelectEmulatorDialog((runCommand) =>
                    {
                        if (runCommand == null)
                        {
                            processView.AppendText($"Emulator not configured, couldn't run {Project.GameString}.gbc.", "error");
                        }
                        else
                        {
                            RunGame(runCommand);
                        }
                    });

                    return true;
                }

                return false;
            });
    }

    void RunGame(string runCommand)
    {
        if (runCommand == null)
            throw new Exception("RunGame(): runCommand not specified");

        var (fileName, arguments) = SubstituteString(runCommand);

        processView.AppendText("Attempting to run with the following command (reconfigure with Misc -> Select Emulator)...");
        processView.AppendText($"\"{fileName}\" " + arguments + '\n', "code");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = Project.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        emulatorProcess = new Process { StartInfo = startInfo };
        emulatorProcess.EnableRaisingEvents = true;
        emulatorProcess.Exited += OnEmulatorExited;

        try
        {
            if (!processView.AttachAndStartProcess(emulatorProcess))
            {
                processView.AppendText("Error: Emulator process could not be started.", "error");
                return;
            }
        }
        catch (Win32Exception)
        {
            processView.AppendText("Error: Emulator process could not be started.", "error");
            return;
        }

        // No apparent error, update global config with the run command
        GlobalConfig.EmulatorCommand = runCommand;
        GlobalConfig.Save();

        // Kill existing emulator process if it exists
        Workspace.RegisterEmulatorProcess(emulatorProcess);
    }

    (string, string) SubstituteString(string s)
    {
        s = s.Replace("{GAME}", Project.GameString);

        // Old format: space separator
        if (!s.Contains("|"))
            return (s.Split()[0], string.Join(" ", s.Split().Skip(1)));

        // New format: "|" symbol separates process name from arguments (supports space in path)
        return (s.Split("|")[0].Trim(), s.Substring(s.IndexOf('|') + 1).Trim());
    }

    void OnEmulatorExited(object sender, object args)
    {
        Helper.MainThreadInvoke(OnEmulatorExitedBody);
    }

    void OnEmulatorExitedBody()
    {
        // Save config so that "close run dialog with emulator" value gets updated
        GlobalConfig.Save();

        if (GlobalConfig.CloseRunDialogWithEmulator)
        {
            Close();
        }
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    public static void SelectEmulatorDialog(Action<string> onSelected)
    {
        bool callbackReceived = false;
        string runCommand = null;

        SelectEmulator((str) =>
        {
            runCommand = str;
            callbackReceived = true;
        });

        Modal.OpenModal("Emulator file dialog", () =>
        {
            ImGui.Text("Waiting for file dialog...");

            if (callbackReceived)
            {
                onSelected(runCommand);
                return true;
            }
            return false;
        });
    }

    static void SelectEmulator(Action<string> onSelected)
    {
        (string, string)[] filterList;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            filterList = new (string, string)[] {("Executable files (.exe)", "exe"), ("All files", "*")};
        else
            filterList = new (string, string)[] {};

        Top.Backend.ShowOpenFileDialog(null, filterList, (selectedFile) =>
        {
            if (selectedFile != null)
                onSelected(selectedFile + " | {GAME}.gbc");
            else
                onSelected(null);
        });
    }
}
