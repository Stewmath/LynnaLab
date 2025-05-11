namespace LynnaLab;

/// <summary>
/// A dialog for editing the settings in GlobalConfig.cs.
/// </summary>
public class SettingsDialog : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public SettingsDialog(string name) : base(name)
    {

    }

    // ================================================================================
    // Variables
    // ================================================================================

    float newScale = 0.0f;

    // ================================================================================
    // Properties
    // ================================================================================

    GlobalConfig GlobalConfig { get { return TopLevel.GlobalConfig; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        Func<string, bool> beginSection = (string name) =>
        {
            ImGui.SeparatorText(name);
            return true;
        };
        Action endSection = () =>
        {
            ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));
        };

        ImGui.Separator();
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));
        ImGuiX.TextCentered("LynnaLab Settings");
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));
        ImGui.Separator();
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));

        if (beginSection("Display"))
        {
            ImGuiX.Checkbox("Light Mode", GlobalConfig.LightMode, (value) =>
            {
                GlobalConfig.LightMode = value;
                ImGuiX.UpdateStyle();
            });

            ImGuiX.Checkbox("Bicubic filter", GlobalConfig.Interpolation == Interpolation.Bicubic, (value) =>
            {
                GlobalConfig.Interpolation = value ? Interpolation.Bicubic : Interpolation.Nearest;
            });

            ImGuiX.Checkbox("Darken duplicate rooms",
                            new Accessor<bool>(() => GlobalConfig.DarkenDuplicateRooms));
            ImGuiX.TooltipOnHover("Rooms which are darkened have a more \"canonical\" version somewhere else, either on the dungeon tab or in a different world index. Duplicate rooms may be missing their warp data.");

            ImGuiX.Checkbox(
                "Hover preview",
                new Accessor<bool>(() => GlobalConfig.ShowBrushPreview
                ));

            ImGuiX.Checkbox("Override system scaling", GlobalConfig.OverrideSystemScaling, (value) =>
            {
                TopLevel.DoNextFrame(() =>
                {
                    GlobalConfig.OverrideSystemScaling = value;
                    if (value)
                        GlobalConfig.DisplayScaleFactor = TopLevel.Backend.WindowDisplayScale;
                    TopLevel.ReAdjustScale();
                });
            });
            ImGuiX.TooltipOnHover("If checked, ignore system preferences and use a manual scale factor for the UI.");
            if (GlobalConfig.OverrideSystemScaling)
            {
                float scale = ImGuiX.ScaleUnit;
                var flags = ImGuiSliderFlags.None;
                ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
                ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(20.0f, 0.0f));
                if (ImGui.SliderFloat("Display scale", ref scale, 1.0f, 4.0f, "%.1f", flags))
                {
                    newScale = scale;
                }
                ImGui.PopItemWidth();
            }
            endSection();
        }

        if (beginSection("Build & Run dialog"))
        {
            if (ImGui.Button("Choose emulator path..."))
            {
                BuildDialog.SelectEmulatorDialog((cmd) =>
                {
                    if (cmd != null)
                        GlobalConfig.EmulatorCommand = cmd;
                });
            }
            ImGui.SameLine();
            ImGui.Text("?");
            ImGuiX.TooltipOnHover(GlobalConfig.EmulatorCommand == null
                                  ? "UNSET"
                                  : GlobalConfig.EmulatorCommand.Replace(" | ", " "));

            ImGuiX.Checkbox("Closing emulator closes dialog", new Accessor<bool>(() => GlobalConfig.CloseRunDialogWithEmulator));
            ImGuiX.Checkbox("Closing dialog closes emulator", new Accessor<bool>(() => GlobalConfig.CloseEmulatorWithRunDialog));
            endSection();
        }

        if (beginSection("Other"))
        {
            ImGuiX.Checkbox("Auto-Adjust World Number",
                                    new Accessor<bool>(() => GlobalConfig.AutoAdjustGroupNumber));
            ImGuiX.TooltipOnHover("The subrosia map & dungeons have duplicates in the World tab. Check this box to auto-adjust the group number to its expected value when selecting these rooms.");

            ImGuiX.Checkbox("Scroll to Zoom", new Accessor<bool>(() => GlobalConfig.ScrollToZoom));
            ImGuiX.TooltipOnHover("Applies to minimaps, scratchpad, and tileset editor.");

            endSection();
        }

        ImGui.Separator();

        if (ImGui.Button("Save settings"))
        {
            GlobalConfig.Save();
            TopLevel.DisplayTimedMessageModal("SaveSettings", $"Settings saved to global_config.yaml!");
        }

        // Only update scale when the mouse is released
        if (newScale != 0.0f && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            float value = newScale; // Capture current value for closure
            TopLevel.DoNextFrame(() =>
            {
                GlobalConfig.DisplayScaleFactor = value;
                TopLevel.ReAdjustScale();
            });

            newScale = 0.0f;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
