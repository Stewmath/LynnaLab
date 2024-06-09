using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using LynnaLib;

namespace LynnaLab
{
    /// Dialog window for building oracles-disasm and then running the result
    public class BuildDialog : Gtk.Dialog
    {
        MainWindow mainWindow;
        ProcessOutputView processView;

        Process makeProcess;
        Process emulatorProcess;

        Gtk.CheckButton closeCheckBox;
        bool makeLaunchFailed;

        public Project Project
        {
            get
            {
                return mainWindow.Project;
            }
        }

        public BuildDialog(MainWindow parent)
            : base("Building project", parent.Window, Gtk.DialogFlags.DestroyWithParent, Gtk.Stock.Close, Gtk.ResponseType.Close)
        {
            this.mainWindow = parent;

            string makeCommand = mainWindow.GlobalConfig.MakeCommand;

            if (makeCommand == null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    makeCommand = "C:/msys64/msys2_shell.cmd -here -no-start -defterm -ucrt64 -shell bash -c \"make {GAME}\"";
                }
                else
                {
                    makeCommand = "/usr/bin/make {GAME}";
                }

                mainWindow.GlobalConfig.MakeCommand = makeCommand;
            }

            makeCommand = SubstituteString(makeCommand);

            var startInfo = new ProcessStartInfo
            {
                FileName = makeCommand.Split()[0],
                Arguments = string.Join(" ", makeCommand.Split().Skip(1)),
                WorkingDirectory = Project.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            // When using quickstart, the environment variable EXTRA_DEFINES is
            // passed to the assembler by the makefile to set the position
            if (mainWindow.QuickstartData.enabled)
            {
                string definitions = "";
                var q = mainWindow.QuickstartData;
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
            makeProcess.Exited += (e, a) => Gtk.Application.Invoke((e2, a2) => OnMakeExited());

            processView = new ProcessOutputView();
            processView.MarginBottom = 6;

            var topLabel = new Gtk.Label("Building oracles-disasm. The first time will take several minutes.");
            topLabel.Halign = Gtk.Align.Start;
            topLabel.MarginBottom = 6;

            var scrolledWindow = new Gtk.ScrolledWindow();
            scrolledWindow.Add(processView);
            scrolledWindow.ShadowType = Gtk.ShadowType.EtchedIn;
            scrolledWindow.SetPolicy(Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
            scrolledWindow.SetSizeRequest(600, 300);
            scrolledWindow.Hexpand = true;
            scrolledWindow.Vexpand = true;

            // Scroll down when text is added to processView
            processView.SizeAllocated += (e, a) =>
            {
                var adjustment = scrolledWindow.Vadjustment;
                adjustment.Value = adjustment.Upper - adjustment.PageSize;
            };

            closeCheckBox = new Gtk.CheckButton("Close dialog with emulator");
            closeCheckBox.Active = mainWindow.GlobalConfig.CloseRunDialogWithEmulator;

            // Assemble the dialog
            this.ContentArea.Add(topLabel);
            this.ContentArea.Add(scrolledWindow);
            this.ContentArea.Add(closeCheckBox);
            this.ShowAll();

            this.Response += (o, a) =>
            {
                if (!makeLaunchFailed)
                    makeProcess.Kill();
                makeProcess.Close();
                this.Destroy();
            };

            // Attempt to build disassembly
            processView.AppendText("Building with command:");
            processView.AppendText(makeCommand, "code");
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
                processView.AppendText("Failed to launch make process with command:", "red");
                processView.AppendText(makeCommand);
                processView.AppendText("\nIf you're on Windows, MSYS2 must be installed.");
                return;
            }
        }

        void OnMakeExited()
        {
            if (makeProcess.ExitCode != 0)
            {
                processView.AppendText($"\nError: make exited with code {makeProcess.ExitCode}", "error");
                return;
            }

            processView.AppendText("\nBuild completed successfully!\n", "success");

            string runCommand = mainWindow.GlobalConfig.EmulatorCommand;

            if (runCommand == null)
            {
                string emulatorPrompt = null;
                if ((emulatorPrompt = mainWindow.PromptForEmulator(true)) == null)
                {
                    processView.AppendText($"Emulator not configured, couldn't run {Project.GameString}.gbc.", "error");
                    return;
                }
                runCommand = emulatorPrompt + " {GAME}.gbc";
            }

            string fullCommand = SubstituteString(runCommand);

            processView.AppendText("Attempting to run with the following command (reconfigure with File -> Select Emulator)...");
            processView.AppendText(fullCommand + '\n', "code");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fullCommand.Split()[0],
                Arguments = string.Join(" ", fullCommand.Split().Skip(1)),
                WorkingDirectory = Project.BaseDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            emulatorProcess = new Process { StartInfo = startInfo };
            emulatorProcess.EnableRaisingEvents = true;
            emulatorProcess.Exited += (e, a) => Gtk.Application.Invoke((e2, a2) => OnEmulatorExited());

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
            mainWindow.GlobalConfig.EmulatorCommand = runCommand;
            mainWindow.GlobalConfig.Save();

            // Kill existing emulator process if it exists
            mainWindow.RegisterEmulatorProcess(emulatorProcess);
        }

        string SubstituteString(string s)
        {
            return s.Replace("{GAME}", Project.GameString);
        }

        void OnEmulatorExited()
        {
            mainWindow.GlobalConfig.CloseRunDialogWithEmulator = closeCheckBox.Active;
            mainWindow.GlobalConfig.Save();

            if (closeCheckBox.Active)
            {
                this.Destroy();
            }
        }
    }
}
