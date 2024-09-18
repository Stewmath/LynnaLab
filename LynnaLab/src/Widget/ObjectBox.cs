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

        objectGroupEventWrapper.Bind<EventArgs>("ModifiedEvent", OnObjectGroupModified, weak: false);
        SetObjectGroup(group);
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

    void OnObjectGroupModified(object sender, EventArgs args)
    {
        MaxIndex = ObjectGroup.GetNumObjects() - 1;
    }

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
}
