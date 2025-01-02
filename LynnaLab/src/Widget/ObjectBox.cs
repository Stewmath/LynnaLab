namespace LynnaLab;

/// <summary>
/// A "Box" of objects in an ObjectGroup which can be selected or reordered.
/// </summary>
public class ObjectBox : SelectionBox
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public ObjectBox(ObjectGroupEditor objectGroupEditor, string name, ObjectGroup group) : base(name)
    {
        base.Unselectable = true;
        base.RectThickness = 1.0f;

        this.ObjectGroupEditor = objectGroupEditor;

        objectGroupEventWrapper.Bind<EventArgs>(
            "StructureModifiedEvent",
            (sender, _) => ReloadObjectGroup(),
            weak: false);
        SetObjectGroup(group);

        base.AddMouseAction(
            MouseButton.RightClick,
            MouseModifier.None,
            MouseAction.Click,
            GridAction.Callback,
            (s, arg) =>
            {
                if (arg.selectedIndex != -1)
                {
                    base.SelectedIndex = arg.selectedIndex;
                    ImGui.OpenPopup("ObjectPopupMenu");
                }
            }
        );
    }

    // ================================================================================
    // Variables
    // ================================================================================

    EventWrapper<ObjectGroup> objectGroupEventWrapper = new EventWrapper<ObjectGroup>();

    // ================================================================================
    // Properties
    // ================================================================================

    public ObjectGroupEditor ObjectGroupEditor { get; private set; }
    public ProjectWorkspace Workspace { get { return ObjectGroupEditor.Workspace; } }
    public ObjectGroup ObjectGroup { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        // Render tile grid & handle most inputs
        base.Render();

        // Popup menu (right clicked on an object)
        if (GetSelectedObject() != null)
        {
            if (ImGui.BeginPopup("ObjectPopupMenu"))
            {
                ObjectPopupMenu(GetSelectedObject(), ObjectGroupEditor.RoomEditor.Room, ObjectGroupEditor.Workspace);
                ImGui.EndPopup();
            }
        }
    }

    public void SetObjectGroup(ObjectGroup group)
    {
        ObjectGroup = group;
        objectGroupEventWrapper.ReplaceEventSource(group);

        ReloadObjectGroup();
    }

    public ObjectDefinition GetSelectedObject()
    {
        if (SelectedIndex == -1)
            return null;
        return ObjectGroup.GetObject(SelectedIndex);
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    // SelectionBox overrides

    protected override void OnMoveSelection(int oldIndex, int newIndex)
    {
        ObjectGroup.MoveObject(oldIndex, newIndex);
    }

    protected override void RenderPopupMenu()
    {
        // List of object types that can be added manually (omits "Pointer" types which are
        // managed automatically)
        ObjectType[] objectTypes = {
            ObjectType.Condition,
            ObjectType.Interaction,
            ObjectType.RandomEnemy,
            ObjectType.SpecificEnemyA,
            ObjectType.SpecificEnemyB,
            ObjectType.Part,
            ObjectType.ItemDrop,
        };

        if (ImGui.Selectable("Paste", false, Workspace.CopiedObject == null ? ImGuiSelectableFlags.Disabled: 0))
        {
            ObjectGroup.AddObjectClone(Workspace.CopiedObject);
        }

        if (ImGui.BeginMenu("Add Object..."))
        {
            foreach (var objType in objectTypes)
            {
                var name = ObjectGroupEditor.ObjectNames[(int)objType];

                if (ImGui.MenuItem(name))
                {
                    SelectedIndex = ObjectGroup.AddObject(objType);
                }
            }
            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    protected override void TileDrawer(int index)
    {
        if (index >= ObjectGroup.GetNumObjects())
            return;

        ObjectDefinition obj = ObjectGroup.GetObject(index);

        Color color = ObjectGroupEditor.GetObjectColor(obj.GetObjectType());
        FRect rect = base.TileRect(index);
        base.AddRectFilled(rect, color);
        DrawObject(obj, 9, 9);
    }



    // ================================================================================
    // Private methods
    // ================================================================================

    void DrawObject(ObjectDefinition obj, float x, float y)
    {
        if (obj.GetGameObject() != null)
        {
            try
            {
                var topLeft = ImGui.GetCursorScreenPos();
                ObjectAnimationFrame o = obj.GetGameObject().DefaultAnimation.GetFrame(0);
                var spriteDrawer = (Bitmap sprite, int xOffset, int yOffset) =>
                {
                    var offset = new Vector2(x + xOffset, y + yOffset);
                    Image image = TopLevel.ImageFromBitmapTracked(sprite);
                    ImGui.SetCursorScreenPos(topLeft + offset * Scale);
                    ImGuiX.DrawImage(image, Scale);
                };
                o.Draw(spriteDrawer);
            }
            catch (NoAnimationException)
            {
                // No animation defined
            }
            catch (InvalidAnimationException)
            {
                // Error parsing an animation
            }
        }
    }

    void ReloadObjectGroup()
    {
        if (ObjectGroup.GetNumObjects() == 0)
        {
            base.MaxIndex = 0;
            base.Selectable = false;
        }
        else
        {
            base.MaxIndex = ObjectGroup.GetNumObjects();
            base.Selectable = true;
        }
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    public static void ObjectPopupMenu(ObjectDefinition def, Room room, ProjectWorkspace workspace)
    {
        if (ImGui.Selectable("Info"))
        {
            workspace.ShowDocumentation(def.GetIDDocumentation());
        }
        if (ImGui.Selectable("Copy"))
        {
            workspace.CopiedObject = def;
        }
        if (ImGui.BeginMenu("Move to..."))
        {
            foreach (ObjectGroup group in room.GetObjectGroup().GetAllGroups())
            {
                var (name, _) = ObjectGroupEditor.GetGroupName(group);
                if (group != def.ObjectGroup && ImGui.MenuItem(name))
                {
                    room.Project.BeginTransaction("Move object");
                    group.AddObjectClone(def);
                    def.Remove();
                    room.Project.EndTransaction();
                }
            }
            ImGui.EndMenu();
        }
        if (ImGui.Selectable("Clone"))
        {
            int i = def.ObjectGroup.AddObjectClone(def);
        }
        if (ImGui.Selectable("Delete"))
        {
            def.Remove();
        }
    }
}
