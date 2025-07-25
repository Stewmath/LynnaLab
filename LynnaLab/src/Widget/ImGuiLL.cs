namespace LynnaLab;

/// <summary>
/// LynnaLab-specific helper functions for ImGui.
/// </summary>
public class ImGuiLL
{
    public static float ENTRY_ITEM_WIDTH { get { return ImGuiX.Unit(120.0f); } }

    /// <summary>
    /// Displays a ComboBox along with (optionally) the raw integer field as an alternate way to set
    /// the value. Returns true if "?" button for documentation was clicked.
    /// </summary>
    public static bool ComboBoxFromConstants(
        string name, ConstantsMapping mapping, ref int value,
        bool withIntInput, bool omitPrefix)
    {
        bool openDoc = false;

        string getLabelName(string name, ConstantsMapping mapping, bool omitPrefix)
        {
            if (!omitPrefix)
                return name;
            return mapping.RemovePrefix(name);
        }

        ImGui.PushItemWidth(ENTRY_ITEM_WIDTH);
        ImGui.BeginGroup();

        if (withIntInput)
        {
            ImGuiX.InputHex($"{name}##InputHex", ref value);
            ImGui.SameLine();
            if (ImGui.Button($"?##{name}"))
            {
                openDoc = true;
            }
        }

        // Calculate width of largest text item
        float width = ImGuiX.Unit(40.0f);
        foreach (string v in mapping.GetAllStrings())
            width = Math.Max(width, ImGui.CalcTextSize(getLabelName(v, mapping, omitPrefix)).X);

        ImGui.PushItemWidth(width + ImGuiX.Unit(35.0f));

        float horizontalShift = ImGuiX.Unit(10.0f);
        ImGuiX.ShiftCursorScreenPos(horizontalShift, 0.0f);

        string preview = "";
        if (mapping.HasValue(value))
            preview = getLabelName(mapping.ByteToString(value), mapping, omitPrefix);
        if (ImGui.BeginCombo($"##{name}-Combobox", preview))
        {
            foreach (string v in mapping.GetAllStrings().OrderBy(v => v))
            {
                int i = mapping.StringToByte(v);
                bool selected = i == value;
                if (ImGui.Selectable(getLabelName(v, mapping, omitPrefix), selected))
                {
                    value = i;
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.PopItemWidth();
        ImGui.EndGroup();
        ImGui.PopItemWidth();

        return openDoc;
    }

    public static bool ComboBoxFromConstants(ValueReferenceDescriptor desc, bool withIntInput = true, bool omitPrefix = false)
    {
        ConstantsMapping mapping = desc.ConstantsMapping;

        int initial = desc.GetIntValue();
        int value = initial;
        bool showDoc = ComboBoxFromConstants(desc.Name, desc.ConstantsMapping, ref value, withIntInput, omitPrefix);
        if (value != initial)
            desc.SetValue(value);
        return showDoc;
    }

    /// <summary>
    /// Displays an interface for editing the given ValueReferenceGroup.
    /// </summary>
    public static void RenderValueReferenceGroup(ValueReferenceGroup vrg, ISet<string> linebreaks, Action<Documentation> showDoc, int maxRows = 8)
    {
        ImGui.PushItemWidth(ENTRY_ITEM_WIDTH);

        int rowCount = 0;
        ImGui.BeginGroup();

        foreach (ValueReferenceDescriptor desc in vrg.GetDescriptors())
        {
            if (!desc.Editable)
                continue;

            if (rowCount >= maxRows || (linebreaks != null && linebreaks.Contains(desc.Name)))
            {
                rowCount = 0;
                ImGui.EndGroup();
                ImGui.SameLine();
                ImGui.BeginGroup();
            }

            var vr = desc.ValueReference;
            switch (desc.ValueType)
            {
                case ValueReferenceType.Bool:
                    ImGuiX.Checkbox(desc.Name, vr.GetIntValue() != 0, (value) =>
                    {
                        vr.SetValue(value ? 1 : 0);
                    });
                    break;
                case ValueReferenceType.Int:
                    if (desc.ConstantsMapping != null)
                    {
                        if (ComboBoxFromConstants(desc))
                            showDoc(desc.Documentation);
                    }
                    else
                    {
                        ImGuiX.InputHex(desc.Name, vr.GetIntValue(), (value) =>
                        {
                            vr.SetValue(value);
                        },
                        min: vr.MinValue, max: vr.MaxValue);
                    }
                    break;
                case ValueReferenceType.String:
                    string data = desc.GetStringValue();
                    if (ImGui.InputText(desc.Name, ref data, 100))
                        desc.SetValue(data);
                    break;
                default:
                    throw new Exception("Unsupported ValueReferenceType: " + desc.ValueType);
            }
            // TODO: Other types

            if (desc.Tooltip != null && ImGui.IsItemHovered())
            {
                ImGuiX.Tooltip(desc.Tooltip);
            }

            rowCount++;
        }

        ImGui.EndGroup();
        ImGui.PopItemWidth();
    }

    /// <summary>
    /// Render all palettes in a palette header group as buttons that can be clicked to change them.
    /// Note: PaletteHeaderData.GetColor() is inefficient, not great to call it every frame
    /// </summary>
    public static void RenderPaletteHeader(PaletteHeaderGroup phg, int paletteToHighlight, ProjectWorkspace workspace)
    {
        if (phg == null)
        {
            ImGui.Text("ERROR: Could not load palettes (invalid index?).");
            return;
        }

        float COLUMN_WIDTH = ImGuiX.Unit(30.0f);
        int callCount = 0;
        bool openRightClickPopup = false;
        Project project = workspace.Project;

        var displayPalette = (PaletteHeaderData data) =>
        {
            string name = data.PointerName;
            string t = data.PaletteType == PaletteType.Background ? "BG" : "SPR";

            ImGui.SeparatorText(name);
            ImGui.BeginChild($"{name}-{callCount} child", new Vector2(COLUMN_WIDTH * 9, 0.0f));
            if (ImGui.BeginTable(name, 9))
            {
                // Number headings
                ImGui.TableNextRow();
                for (int p = 0; p < 8; p++)
                {
                    ImGui.TableSetColumnIndex(p);
                    ImGuiX.TextCentered(p.ToString());
                }

                // For each row
                for (int i = 0; i < 4; i++)
                {
                    ImGui.TableNextRow();

                    // For each column
                    for (int p = data.FirstPalette; p < data.FirstPalette + data.NumPalettes; p++)
                    {
                        ImGui.TableSetColumnIndex(p);

                        // Set the background to blue when rendering the selected palette
                        if (p == paletteToHighlight)
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, Color.Blue.ToUInt());
                        else
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.CellBg, ImGui.GetColorU32(ImGuiCol.TableRowBg));

                        if (data.IsResolvable)
                        {
                            // Color picker button
                            string label = $"{t}-{p}-{i} ({name})##{callCount}";
                            ImGuiX.ColorEdit(label, data.GetColor(p, i), (color) =>
                            {
                                project.BeginTransaction($"Set color#{name}-{t}-{p}-{i}-{callCount}", true);
                                data.SetColor(p, i, color);
                                project.EndTransaction();
                            });
                            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                            {
                                openRightClickPopup = true;
                                paletteButtonPopupData = data;
                                paletteButtonPopupPalette = p;
                                paletteButtonPopupIndex = i;
                            }
                        }
                        else
                        {
                            ImGuiX.TextCentered("X");
                            if (ImGui.IsItemHovered())
                                ImGui.SetTooltip("Failed to load");
                        }
                    }
                }

                ImGui.EndTable();
            }
            ImGui.EndChild();

            callCount++;
        };

        string sectionName = null;

        ImGui.BeginGroup();

        phg.Foreach((data) =>
        {
            string name = data.PaletteType == PaletteType.Background ? "BG" : "SPR";
            if (name != sectionName)
            {
                if (sectionName != null)
                    ImGui.EndGroup();
                ImGui.BeginGroup();
                sectionName = name;
                ImGui.SeparatorText(name + " Palettes");
            }

            displayPalette(data);
        });

        ImGui.EndGroup();
        ImGui.EndGroup();

        // Right-click popup menu
        if (openRightClickPopup)
            ImGui.OpenPopup("Palette Button Popup");
        if (ImGui.BeginPopup("Palette Button Popup"))
        {
            if (ImGui.Selectable("Copy"))
            {
                Color c = paletteButtonPopupData.GetColor(paletteButtonPopupPalette, paletteButtonPopupIndex);
                Top.SetClipboardData(Color.MimeType, c.Serialize());
            }
            if (ImGui.Selectable("Paste", false, !Top.HasClipboardData(Color.MimeType) ? ImGuiSelectableFlags.Disabled : 0))
            {
                string colorString = Top.GetClipboardData(Color.MimeType);
                Color? color = Color.Deserialize(colorString);

                if (color == null)
                {
                    Modal.DisplayErrorMessage($"Failed to parse color from clipboard: \"{colorString}\"");
                }
                else
                {
                    project.BeginTransaction("Copy palette");
                    paletteButtonPopupData.SetColor(paletteButtonPopupPalette, paletteButtonPopupIndex, (Color)color);
                    project.EndTransaction();
                }
            }
            ImGui.EndPopup();
        }
    }

    static PaletteHeaderData paletteButtonPopupData;
    static int paletteButtonPopupPalette;
    static int paletteButtonPopupIndex;

    static readonly HashSet<string> tilesetPropsLineBreaks = new HashSet<string>(
        "Dungeon Index".Split(","));

    public static void RenderTilesetFields(Tileset tileset, Action<Documentation> showDoc)
    {
        RenderValueReferenceGroup(tileset.ValueReferenceGroup, tilesetPropsLineBreaks, showDoc);
    }

    /// <summary>
    /// Fields to choose a tileset (index + season).
    /// </summary>
    public static void TilesetChooser(Project project, string name, int index, Season season, Action<int, Season> onChanged)
    {
        ImGui.BeginGroup();
        ImGui.PushItemWidth(ENTRY_ITEM_WIDTH);

        var handleChanged = (int index, Season season) =>
        {
            onChanged(index, project.ValidateTilesetSeason(index, season, autoCorrect: true));
        };

        ImGuiX.InputHex($"##{name}-Tileset-Index", index, (value) =>
        {
            handleChanged(value, season);
        }, max: project.NumTilesets-1);

        if (project.TilesetIsSeasonal(index))
        {
            ImGui.SameLine();

            int newSeason = (int)season;
            ComboBoxFromConstants($"##{name}-Tileset-Season", project.SeasonMapping, ref newSeason,
                                  withIntInput: false, omitPrefix: true);
            if (newSeason != (int)season)
                handleChanged(index, (Season)newSeason);
        }

        ImGui.PopItemWidth();
        ImGui.EndGroup();
    }
}
