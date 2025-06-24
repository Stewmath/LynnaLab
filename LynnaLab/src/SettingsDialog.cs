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
    bool fontSizeChanged = false;

    // ================================================================================
    // Properties
    // ================================================================================

    GlobalConfig GlobalConfig { get { return Top.GlobalConfig; } }

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
            ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 20.0f));
        };

        ImGui.Separator();
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));
        ImGuiX.TextCentered("LynnaLab Settings");
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));
        ImGui.Separator();
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(0.0f, 10.0f));

        if (beginSection("Display"))
        {
            int filter = (int)GlobalConfig.Interpolation;
            if (ImGui.Combo("Filter", ref filter, new string[] { "Nearest", "Bilinear", "Bicubic" }, 3))
            {
                GlobalConfig.Interpolation = (Interpolation)filter;
            }
            ImGuiX.TooltipOnHover("How to handle non-integer scaling (mainly on minimaps).");

            ImGuiX.Checkbox("Light Mode", GlobalConfig.LightMode, (value) =>
            {
                GlobalConfig.LightMode = value;
                ImGuiX.UpdateStyle();
            });

            ImGuiX.Checkbox("Darken duplicate rooms",
                            new Accessor<bool>(() => GlobalConfig.DarkenDuplicateRooms));
            ImGuiX.TooltipOnHover("Rooms which are darkened have a more \"canonical\" version somewhere else, either on the dungeon tab or in a different world index. Duplicate rooms may be missing their warp data.");

            ImGuiX.Checkbox(
                "Hover preview",
                new Accessor<bool>(() => GlobalConfig.ShowBrushPreview
                ));

            ImGuiX.Checkbox(
                "Coordinate tooltips",
                new Accessor<bool>(() => GlobalConfig.ShowCoordinateTooltip
                ));
            ImGuiX.TooltipOnHover("Show coordinate tooltips in room editor and minimap.");

            ImGuiX.Checkbox("Override system scaling", GlobalConfig.OverrideSystemScaling, (value) =>
            {
                Top.DoNextFrame(() =>
                {
                    GlobalConfig.OverrideSystemScaling = value;
                    if (value)
                        GlobalConfig.DisplayScaleFactor = Top.Backend.WindowDisplayScale;
                    Top.ReAdjustScale();
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

        if (beginSection("Fonts"))
        {
            // Subfunction to display options for one of the two font types
            var fontOptions = (Accessor<string> fontName, Accessor<int> size, string name, string tooltip, bool menu) =>
            {
                ImGui.BeginGroup();

                // Combo box (not using the simpler "Combo()" function so I can change the font of
                // each entry)
                var oldFont = Top.GetFont(fontName.Get());
                ImGui.PushFont(menu ? oldFont.menuSize : oldFont.infoSize);
                if (ImGui.BeginCombo($"##{name}", oldFont.name))
                {
                    ImGui.PopFont();
                    foreach (string v in Top.AvailableFonts.OrderBy(v => v))
                    {
                        var font = Top.GetFont(v);
                        bool selected = v == oldFont.name;
                        ImGui.PushFont(font.menuSize);
                        if (ImGui.Selectable(v, selected))
                            fontName.Set(v);
                        if (selected)
                            ImGui.SetItemDefaultFocus();
                        ImGui.PopFont();
                    }
                    ImGui.EndCombo();
                }
                else
                    ImGui.PopFont();

                // Text on the right + tooltip
                float width = ImGui.GetItemRectSize().X;
                ImGui.SameLine();
                ImGui.Text(name);
                ImGui.EndGroup();
                ImGuiX.TooltipOnHover(tooltip);

                // Size slider
                float offset = ImGuiX.Unit(20.0f);
                ImGuiX.ShiftCursorScreenPos(offset, 0.0f);
                ImGui.PushItemWidth(width - offset);
                if (ImGuiX.SliderInt($" Size##{name}", size, 10, 40))
                    fontSizeChanged = true;
                ImGui.PopItemWidth();
            };

            fontOptions(new(() => GlobalConfig.MenuFont),
                        new(() => GlobalConfig.MenuFontSize),
                        "Menu font",
                        "Font to use for menus, labels, etc.",
                        true);

            fontOptions(new(() => GlobalConfig.InfoFont),
                        new(() => GlobalConfig.InfoFontSize),
                        "Info font",
                        "Font to use for tooltips, documentation, etc.",
                        false);

            endSection();
        }

        if (beginSection("Build & Run dialog"))
        {
            ImGuiX.Checkbox("Closing emulator closes dialog", new Accessor<bool>(() => GlobalConfig.CloseRunDialogWithEmulator));
            ImGuiX.Checkbox("Closing dialog closes emulator", new Accessor<bool>(() => GlobalConfig.CloseEmulatorWithRunDialog));
            endSection();
        }

        if (beginSection("External programs"))
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

            if (ImGui.Button("Choose PNG editor path..."))
            {
                Top.Backend.ShowOpenFileDialog(null, Top.ProgramFileFilter, (selectedFile) =>
                {
                    if (selectedFile != null)
                        GlobalConfig.EditPngProgram = selectedFile;
                });
            }
            ImGui.SameLine();
            ImGui.Text("?");
            ImGuiX.TooltipOnHover(GlobalConfig.EditPngProgram == null
                                  ? "UNSET"
                                  : GlobalConfig.EditPngProgram);

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
            Modal.DisplayTimedMessageModal("SaveSettings", $"Settings saved to global_config.yaml!");
        }

        // Only update scale when the mouse is released
        if ((newScale != 0.0f || fontSizeChanged) && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            float value = newScale; // Capture current value for closure
            Top.DoNextFrame(() =>
            {
                if (value != 0.0f)
                    GlobalConfig.DisplayScaleFactor = value;
                Top.ReAdjustScale();
            });

            newScale = 0.0f;
            fontSizeChanged = false;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
