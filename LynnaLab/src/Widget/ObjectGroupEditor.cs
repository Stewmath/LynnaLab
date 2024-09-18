namespace LynnaLab;

public class ObjectGroupEditor : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ObjectGroupEditor(string name, ObjectGroup group) : base(name)
    {
        SetObjectGroup(group);
    }

    // ================================================================================
    // Constants
    // ================================================================================

    public static readonly String[] ObjectNames = {
        "Condition",
        "Interaction",
        "Object Pointer",
        "Before Event Pointer",
        "After Event Pointer",
        "Random Position Enemy",
        "Specific Position Enemy (A)",
        "Specific Position Enemy (B)",
        "Part",
        "Item Drop",
    };

    public static readonly String[] ObjectDescriptions = {
        "Set a condition to enable or disable the following objects.",
        "A class of objects which can use scripts.",
        "A pointer to another set of object data.",
        "A pointer which only activates when bit 7 of the room flags is NOT set.",
        "A pointer which only activates when bit 7 of the room flags IS set.",
        "An enemy (or multiple enemies) in random positions in the room.",
        "An enemy at a specific position in the room. (Can set flags, can't set Var03.)",
        "An enemy at a specific position in the room. (Can't set flags, can set Var03.)",
        "A class of objects with a variety of purposes (switches, animal statues, particles...)",
        "An item drops when a tile is destroyed at a given location."
    };


    // ================================================================================
    // Variables
    // ================================================================================

    ObjectGroup topObjectGroup, selectedObjectGroup;
    ObjectDefinition activeObject;
    int selectedIndex = -1;

    Dictionary<ObjectGroup, ObjectBox> objectBoxDict = new Dictionary<ObjectGroup, ObjectBox>();

    bool disableBoxCallback = false;

    // ================================================================================
    // Properties
    // ================================================================================

    Project Project
    {
        get
        {
            return TopObjectGroup.Project;
        }
    }

    // The TOP-LEVEL object group for this room.
    public ObjectGroup TopObjectGroup
    {
        get { return topObjectGroup; }
    }

    // The object group containing the current selected object.
    public ObjectGroup SelectedObjectGroup
    {
        get { return selectedObjectGroup; }
    }

    public ObjectBox SelectedObjectBox
    {
        get
        {
            if (SelectedObjectGroup == null)
                return null;
            return objectBoxDict[SelectedObjectGroup];
        }
    }

    // The index of the selected object within SelectedObjectGroup.
    public int SelectedIndex
    {
        get
        {
            if (SelectedObjectBox == null)
                return -1;
            return SelectedObjectBox.SelectedIndex;
        }
    }

    public ObjectDefinition SelectedObject
    {
        get
        {
            if (SelectedIndex == -1)
                return null;
            return SelectedObjectGroup.GetObject(SelectedIndex);
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        var childSize = ImGui.GetContentRegionAvail() - new Vector2(0.0f, 200.0f);
        if (SelectedObject == null)
            childSize = new Vector2(0.0f, 0.0f);

        if (ImGui.BeginChild(Name + " Object Boxes", childSize, ImGuiChildFlags.Border))
        {
            if (ImGui.BeginTable(Name + " Table", 1))
            {
                int col = 0;

                foreach (var box in objectBoxDict.Values)
                {
                    if (col == 0)
                        ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    var (name, tooltip) = GetGroupName(box.ObjectGroup);
                    ImGui.SeparatorText(name);
                    ImGuiX.TooltipOnHover(tooltip);

                    if (ImGui.BeginChild(box.ObjectGroup.Identifier + " frame",
                                         box.WidgetSize + new Vector2(12.0f, 12.0f),
                                         ImGuiChildFlags.Border))
                    {
                        box.Render();
                    }
                    ImGui.EndChild();

                    if (++col == 1)
                        col = 0;
                }

                ImGui.EndTable();
            }
        }
        ImGui.EndChild();

        if (SelectedObject != null)
            ImGuiLL.RenderValueReferenceGroup(SelectedObject);
    }

    public void SetObjectGroup(ObjectGroup topObjectGroup)
    {
        if (this.topObjectGroup != null)
            this.topObjectGroup.RemoveModifiedHandler(ObjectGroupModifiedHandler);
        this.topObjectGroup = topObjectGroup;
        this.topObjectGroup.AddModifiedHandler(ObjectGroupModifiedHandler);

        ReloadObjectBoxes();
    }

    public void SelectObject(ObjectGroup group, int index)
    {
        if (!topObjectGroup.GetAllGroups().Contains(group))
            throw new Exception("Tried to select from an invalid object group.");

        index = Math.Min(index, group.GetNumObjects() - 1);

        selectedObjectGroup = group;
        selectedIndex = index;

        disableBoxCallback = true;

        foreach (ObjectGroup g2 in topObjectGroup.GetAllGroups())
        {
            if (g2 == selectedObjectGroup)
                objectBoxDict[g2].SetSelectedIndex(index);
            else
                objectBoxDict[g2].SetSelectedIndex(-1);
        }

        disableBoxCallback = false;

        if (selectedIndex == -1)
            SetObject(null);
        else
            SetObject(selectedObjectGroup.GetObject(selectedIndex));
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void SelectObject(ObjectGroup group, ObjectDefinition obj)
    {
        int index = group.GetObjects().IndexOf(obj);
        SelectObject(group, index);
    }

    void SetObject(ObjectDefinition obj)
    {
        if (activeObject == obj)
            return;
        activeObject = obj;

        // TODO
        // if (RoomEditor != null)
        //     RoomEditor.OnObjectSelected();

        UpdateDocumentation();
    }

    void OnObjectDataModified()
    {
        UpdateDocumentation();
    }

    void ReloadObjectBoxes()
    {
        foreach (var box in objectBoxDict)
        {
            // TODO: Dispose?
        }
        objectBoxDict.Clear();

        foreach (ObjectGroup group in topObjectGroup.GetAllGroups())
        {
            // TODO: Is name ok
            var objectBox = new ObjectBox(group.Identifier, group);

            objectBox.SelectedEvent += (index) =>
            {
                if (!disableBoxCallback)
                    SelectObject(objectBox.ObjectGroup, index);
            };

            objectBoxDict.Add(group, objectBox);
        }

        SelectObject(TopObjectGroup, -1);
    }

    void ObjectGroupModifiedHandler(object sender, EventArgs args)
    {
        if (selectedObjectGroup != null && activeObject != null)
            SelectObject(selectedObjectGroup, activeObject);
    }

    // TODO
    void UpdateDocumentation()
    {
        // // Update tooltips in case ID has changed
        // if (activeObject == null)
        //     return;

        // var editor = ObjectDataEditor;
        // if (editor == null)
        //     return;

        // ValueReference r;
        // try
        // {
        //     r = activeObject.GetValueReference("ID");
        // }
        // catch (InvalidLookupException)
        // {
        //     return;
        // }

        // if (r != null)
        // {
        //     activeObject.GetValueReference("SubID").Documentation = null; // Set it to null now, might replace it below
        //     if (r.ConstantsMapping != null)
        //     {
        //         try
        //         {
        //             // Set tooltip based on ID field documentation
        //             string objectName = r.ConstantsMapping.ByteToString((byte)r.GetIntValue());
        //             string tooltip = objectName + "\n\n";
        //             //tooltip += r.GetDocumentationField("desc");
        //             editor.SetTooltip(r, tooltip.Trim());

        //             Documentation doc = activeObject.GetIDDocumentation();
        //             activeObject.GetValueReference("SubID").Documentation = doc;
        //         }
        //         catch (KeyNotFoundException)
        //         {
        //         }
        //     }
        // }
        // editor.UpdateHelpButtons();
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    public static Color GetObjectColor(ObjectType type)
    {
        switch (type)
        {
            case ObjectType.Condition:
                return Color.Green;
            case ObjectType.Interaction:
                return Color.DarkOrange;
            case ObjectType.RandomEnemy:
                return Color.Purple;
            case ObjectType.SpecificEnemyA:
                return Color.FromRgb(128, 64, 0);
            case ObjectType.SpecificEnemyB:
                return Color.FromRgb(128, 64, 0);
            case ObjectType.Part:
                return Color.Gray;
            case ObjectType.ItemDrop:
                return Color.Lime;

            // These should never be drawn
            case ObjectType.Pointer:
            case ObjectType.BeforeEvent:
            case ObjectType.AfterEvent:
                return Color.Yellow;
        }
        return Color.White; // End, EndPointer, Garbage types should never be drawn
    }

    static (String, String) GetGroupName(ObjectGroup group)
    {
        ObjectGroupType type = group.GetGroupType();

        switch (type)
        {
            case ObjectGroupType.Main:
                return ("Main objects", "Non-enemy objects go here.");
            case ObjectGroupType.Enemy:
                return ("Enemy objects", "Objects that despawn when Maple appears.");
            case ObjectGroupType.BeforeEvent:
                return ("Before event", "Objects that despawn when room flag bit 7 is set, or when Maple appears.");
            case ObjectGroupType.AfterEvent:
                return ("After event", "Objects that despawn when room flag bit 7 is unset, or when Maple appears.");
            case ObjectGroupType.Shared:
                return ("[SHARED] " + group.Identifier, "This object group may used by multiple rooms. Edit with caution.");
        }

        throw new Exception("Unexpected thing happened");
    }
}
