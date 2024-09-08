namespace LynnaLab;

/// <summary>
/// LynnaLab-specific helper functions for ImGui.
/// </summary>
public class ImGuiLL
{
    public const float ENTRY_ITEM_WIDTH = 150.0f;
    public const float COMBO_ITEM_WIDTH = ENTRY_ITEM_WIDTH * 3;

    public static void ComboBoxFromConstants(ValueReferenceDescriptor desc)
    {
        ConstantsMapping mapping = desc.ConstantsMapping;

        ImGui.PushItemWidth(ENTRY_ITEM_WIDTH);
        ImGui.BeginGroup();

        int initial = desc.GetIntValue();

        ImGuiX.InputHex(desc.Name, initial, (value) =>
        {
            desc.SetValue(value);
        });

        ImGui.PushItemWidth(COMBO_ITEM_WIDTH);

        string preview = "";
        if (mapping.HasValue(initial))
            preview = mapping.ByteToString(initial);
        if (ImGui.BeginCombo("##Combobox", preview))
        {
            foreach (string v in mapping.GetAllStrings().OrderBy(v => v))
            {
                int i = mapping.StringToByte(v);
                bool selected = i == initial;
                if (ImGui.Selectable(v, selected))
                {
                    desc.SetValue(i);
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.PopItemWidth();
        ImGui.EndGroup();
        ImGui.PopItemWidth();
    }

    public static void RenderValueReferenceGroup(ValueReferenceGroup vrg, ISet<string> linebreaks)
    {
        ImGui.PushItemWidth(ENTRY_ITEM_WIDTH);

        int maxRows = 8;
        int rowCount = 0;
        ImGui.BeginGroup();

        foreach (ValueReferenceDescriptor desc in vrg.GetDescriptors())
        {
            if (rowCount >= maxRows || linebreaks.Contains(desc.Name))
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
                        ComboBoxFromConstants(desc);
                    }
                    else
                    {
                        ImGuiX.InputHex(desc.Name, vr.GetIntValue(), (value) =>
                        {
                            vr.SetValue(value);
                        });
                    }
                    break;
                default:
                    throw new Exception("Unsupported ValueReferenceType");
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

    // ================================================================================
    // Private methods
    // ================================================================================
}
