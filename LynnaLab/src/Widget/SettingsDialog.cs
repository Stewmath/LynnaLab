#nullable disable
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

    static readonly (string Name, EmulatorKey Key)[] EmulatorButtons = [
        ("Right", EmulatorKey.Right),
        ("Left", EmulatorKey.Left),
        ("Up", EmulatorKey.Up),
        ("Down", EmulatorKey.Down),
        ("A", EmulatorKey.A),
        ("B", EmulatorKey.B),
        ("Select", EmulatorKey.Select),
        ("Start", EmulatorKey.Start),
        ("Speedup", EmulatorKey.Speedup),
    ];

    float newScale = 0.0f;
    int newMenuFontSize = 0;
    EmulatorKey? keybindingBeingAdded;

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
                float displaySize = menu ? GlobalConfig.MenuFontSize : GlobalConfig.InfoFontSize;
                ImGui.PushFont(oldFont.normal, displaySize);
                if (ImGui.BeginCombo($"##{name}", oldFont.name))
                {
                    ImGui.PopFont();
                    foreach (string v in Top.AvailableFonts.OrderBy(v => v))
                    {
                        var font = Top.GetFont(v);
                        bool selected = v == oldFont.name;
                        ImGui.PushFont(font.normal, displaySize);
                        if (ImGui.Selectable(v, selected))
                        {
                            fontName.Set(v);
                            Top.UpdateDefaultFont();
                        }
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
                ImGuiX.SliderInt($" Size##{name}", size, 10, 40);
                ImGui.PopItemWidth();
            };

            fontOptions(new(() => GlobalConfig.MenuFont),
                        new(() => GlobalConfig.MenuFontSize, (v) => { newMenuFontSize = v; }),
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

        if (beginSection("Internal emulator"))
        {
            ImGuiX.Checkbox("Draw object boxes", new Accessor<bool>(() => GlobalConfig.EmulatorDrawObjectBoxes));
            ImGuiX.TooltipOnHover("Objects can be manipulated by clicking and dragging.");
            ImGuiX.Checkbox("Unfocused Input", new Accessor<bool>(() => GlobalConfig.EmulatorUnfocusedInput));
            ImGuiX.TooltipOnHover("Control the emulator while it's not focused.");

            float volume = GlobalConfig.EmulatorVolume;
            if (ImGui.SliderFloat("Volume", ref volume, 0.0f, 1.0f, "%.2f"))
                GlobalConfig.EmulatorVolume = volume;

            int emulatorScale = GlobalConfig.EmulatorScale;
            if (ImGui.SliderInt("Scale", ref emulatorScale, 1, 8))
                GlobalConfig.EmulatorScale = emulatorScale;

            if (ImGui.Button("Edit keybindings..."))
                OpenKeybindingsModal();

            endSection();
        }

        if (beginSection("External programs"))
        {
            ImGuiX.Checkbox("Use external emulator", new Accessor<bool>(() => GlobalConfig.UseExternalEmulator));
            ImGuiX.TooltipOnHover("Choose between LynnaLab's internal emulator and an external one.");

            if (GlobalConfig.UseExternalEmulator)
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
            }

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
        if ((newScale != 0.0f || newMenuFontSize != 0) && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
        {
            float scaleValue = newScale; // Capture current value for closure
            int menuValue = newMenuFontSize;
            Top.DoNextFrame(() =>
            {
                if (scaleValue != 0.0f)
                    GlobalConfig.DisplayScaleFactor = scaleValue;
                if (menuValue != 0)
                    GlobalConfig.MenuFontSize = menuValue;
                Top.ReAdjustScale();
            });

            newScale = 0.0f;
            newMenuFontSize = 0;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void OpenKeybindingsModal()
    {
        keybindingBeingAdded = null;
        Modal.OpenModal("Edit emulator keybindings", RenderKeybindingsModal);
    }

    bool RenderKeybindingsModal()
    {
        var keyMap = GlobalConfig.EmulatorKeybindings.KeyMap;

        ImGui.Text("Internal emulator keybindings");
        ImGui.Separator();

        if (ImGui.BeginTable("Emulator keybindings", 3, ImGuiTableFlags.SizingFixedFit))
        {
            ImGui.TableSetupColumn("Button", ImGuiTableColumnFlags.WidthFixed, ImGuiX.Unit(70.0f));
            ImGui.TableSetupColumn("Key", ImGuiTableColumnFlags.WidthFixed, ImGuiX.Unit(120.0f));
            ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, ImGuiX.Unit(150.0f));
            ImGui.TableHeadersRow();

            foreach (var (name, mask) in EmulatorButtons)
            {
                ImGuiKey? key = keyMap
                    .Where(pair => pair.Value.Equals(mask))
                    .Select(pair => (ImGuiKey?)pair.Key)
                    .FirstOrDefault();

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(name);
                ImGui.TableNextColumn();
                ImGui.Text(key == null ? "Unbound" : key.ToString());
                ImGui.TableNextColumn();

                if (keybindingBeingAdded == mask)
                {
                    ImGui.Text("Press a key...");
                }
                else if (ImGui.SmallButton($"Change##{name}"))
                {
                    keybindingBeingAdded = mask;
                }

                if (key != null && keybindingBeingAdded != mask)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($"Clear##{name}"))
                        keyMap.Remove(key.Value);
                }
            }

            ImGui.EndTable();
        }

        if (keybindingBeingAdded != null)
            CaptureKeybinding(keyMap);

        ImGui.Separator();
        if (ImGui.Button("Restore defaults"))
        {
            GlobalConfig.ResetEmulatorKeybindings();
            keybindingBeingAdded = null;
        }
        ImGui.SameLine();
        if (ImGui.Button("Done"))
            return true;

        return false;
    }

    void CaptureKeybinding(Dictionary<ImGuiKey, EmulatorKey> keyMap)
    {
        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            keybindingBeingAdded = null;
            return;
        }

        foreach (ImGuiKey key in Enum.GetValues<ImGuiKey>())
        {
            if (key < ImGuiKey.NamedKeyBegin || key >= ImGuiKey.NamedKeyEnd)
                continue;
            if (!ImGui.IsKeyPressed(key))
                continue;

            var existingKeys = keyMap
                .Where(pair => pair.Value.Equals(keybindingBeingAdded.Value))
                .Select(pair => pair.Key)
                .ToList();
            foreach (var existingKey in existingKeys)
                keyMap.Remove(existingKey);

            keyMap.Remove(key);
            keyMap[key] = keybindingBeingAdded.Value;
            keybindingBeingAdded = null;
            return;
        }
    }
}
