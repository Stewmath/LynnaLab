namespace LynnaLab;

/// <summary>
/// A "box" allowing one to select items in it, drag them to change ordering, and right-click to
/// bring up a menu.
/// </summary>
public abstract class SelectionBox : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public SelectionBox(string name) : base(name)
    {
        base.Width = 8;
        base.Height = 2;
        base.TileWidth = 18;
        base.TileHeight = 18;
        base.TilePaddingX = 5;
        base.TilePaddingY = 5;

        base.Selectable = true;

        // TODO
        //base.BackgroundColor = Color.FromRgbDbl(0.8, 0.8, 0.8);
        base.HoverColor = Color.Cyan;

        TileGridEventHandler dragCallback = (sender, args) =>
        {
            if (args.selectedIndex == SelectedIndex)
                return;
            if (SelectedIndex != -1 && args.selectedIndex != -1)
                OnMoveSelection(SelectedIndex, args.selectedIndex);
            SelectedIndex = args.selectedIndex;
        };

        base.AddMouseAction(MouseButton.LeftClick, MouseModifier.Any, MouseAction.Drag,
                GridAction.Callback, dragCallback);


        // TODO
        // this.ButtonPressEvent += (sender, args) =>
        // {
        //     if (args.Event.Button == 3)
        //     {
        //         if (HoveringIndex != -1)
        //             SelectedIndex = HoveringIndex;
        //         ShowPopupMenu(args.Event);
        //     }
        // };
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void SetSelectedIndex(int index)
    {
        SelectedIndex = index;
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected abstract void OnMoveSelection(int oldIndex, int newIndex);
    protected abstract void ShowPopupMenu();
}
