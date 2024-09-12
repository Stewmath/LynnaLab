using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LynnaLab;

public class BuildDialog
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public BuildDialog(ProjectWorkspace workspace)
    {
        this.Workspace = workspace;
    }

    // ================================================================================
    // Variables
    // ================================================================================
    ProcessOutputView processView;

    Process makeProcess;
    Process emulatorProcess;

    bool makeLaunchFailed;


    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public bool Visible { get; private set; }

    // Private properties
    GlobalConfig GlobalConfig { get { return TopLevel.GlobalConfig; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render()
    {
        ImGui.PushFont(TopLevel.DefaultFont);

        if (ImGui.BeginChild("Build Dialog ProcessView",
                             new Vector2(0.0f, -40.0f)))
        {
            processView?.Render();
            ImGui.EndChild();
        }

        ImGuiX.Checkbox("Close dialog with emulator",
                        new Accessor<bool>(() => GlobalConfig.CloseRunDialogWithEmulator));
        ImGuiX.Checkbox("Close emulator with dialog",
                        new Accessor<bool>(() => GlobalConfig.CloseEmulatorWithRunDialog));

        ImGui.PopFont();
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
        if (!Visible)
            return;
        HaltMake();
        if (GlobalConfig.CloseEmulatorWithRunDialog)
            HaltEmulator();
        Visible = false;

        // Just in case checkboxes were modified
        GlobalConfig.Save();
    }

    public void BeginCompile()
    {
        // Stop previous compilation if it's in progress
        HaltMake();

        Visible = true;

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

        string runCommand = GlobalConfig.EmulatorCommand;

        if (runCommand == null)
        {
            // TODO: Emulator prompt
            // string emulatorPrompt = null;
            // if ((emulatorPrompt = mainWindow.PromptForEmulator(true)) == null)
            // {
            //     processView.AppendText($"Emulator not configured, couldn't run {Project.GameString}.gbc.", "error");
            //     return;
            // }
            // runCommand = emulatorPrompt;
            throw new NotImplementedException();
        }

        var (fileName, arguments) = SubstituteString(runCommand);

        processView.AppendText("Attempting to run with the following command (reconfigure with File -> Select Emulator)...");
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
}
