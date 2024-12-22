namespace LynnaLab;

/// <summary>
/// A "Box" of objects in an ObjectGroup which can be selected or reordered.
/// </summary>
public class ObjectBox : SelectionBox
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public ObjectBox(string name, ObjectGroup group) : base(name)
    {
        base.Unselectable = true;
        base.RectThickness = 1.0f;

        objectGroupEventWrapper.Bind<EventArgs>(
            "StructureModifiedEvent",
            (sender, _) => SetObjectGroup(sender as ObjectGroup),
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

    public ObjectGroup ObjectGroup { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        // Render tile grid & handle most inputs
        base.Render();

        // Catch right clicks outside any existing components
        ImGui.SetCursorScreenPos(base.origin);
        if (ImGui.InvisibleButton("Background button", base.WidgetSize, ImGuiButtonFlags.MouseButtonRight))
        {
            ImGui.OpenPopup("AddPopupMenu");
        }

        // Popup menu (right clicked on an object)
        if (GetSelectedObject() != null)
        {
            ObjectPopupMenu(GetSelectedObject(), "ObjectPopupMenu");
        }

        // Popup menu (right clicked on an empty spot)
        if (ImGui.BeginPopup("AddPopupMenu"))
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

            ImGui.Text("Add Object...");
            ImGui.Separator();

            foreach (var objType in objectTypes)
            {
                var name = ObjectGroupEditor.ObjectNames[(int)objType];

                if (ImGui.Selectable(name))
                {
                    SelectedIndex = ObjectGroup.AddObject(objType);
                }
            }

            ImGui.EndPopup();
        }
    }

    public void SetObjectGroup(ObjectGroup group)
    {
        ObjectGroup = group;
        objectGroupEventWrapper.ReplaceEventSource(group);

        if (ObjectGroup.GetNumObjects() == 0)
        {
            base.MaxIndex = 0;
            base.Selectable = false;
        }
        else
        {
            base.MaxIndex = ObjectGroup.GetNumObjects() - 1;
            base.Selectable = true;
        }
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

    protected override void ShowPopupMenu()
    {
        // TODO
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
                    Image image = TopLevel.ImageFromBitmap(sprite);
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

    // ================================================================================
    // Static methods
    // ================================================================================

    public static void ObjectPopupMenu(ObjectDefinition def, string name)
    {
        if (ImGui.BeginPopup(name))
        {
            if (ImGui.Selectable("Delete"))
            {
                def.Remove();
            }
            ImGui.EndPopup();
        }
    }
}
