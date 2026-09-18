namespace LynnaLab;

/// <summary>
/// An interface for clients which draw OverlayBoxes.
/// </summary>
public interface IOverlayBoxUser
{
    public bool SelectingRectangle { get; }
    public bool DraggingComponent { get; set; }
    public Vector2 DraggingComponentOffset { get; }
    public OverlayBox Selected { get; }
    public float Scale { get; }

    public void SetSelected(OverlayBox comp, bool startDrag = false, Vector2? dragOffset = null);
    public void SuppressCurrentClick() { }

    // ================================================================================
    // Public static methods
    // ================================================================================

    /// <summary>
    /// Common code for OverlayBox users.
    /// </summary>
    public static OverlayBox? UpdateOverlayBoxes(
        Project project, // Can be null, only for recording undo/redo info
        IOverlayBoxUser user,
        SizedWidget canvas,
        IList<OverlayBox> overlayBoxes,
        bool alwaysFullPrecision,
        bool allowMove = true,
        bool clipToCanvas = true
    )
    {
        OverlayBox? hoveringComponent = null;
        var mousePos = canvas.GetRelativeMousePos() / user.Scale;
        int componentIndex = 0;

        bool isWindowHovered = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);

        // Render room components
        foreach (var com in overlayBoxes)
        {
            FRect? rect = new FRect(com.BoxRectangle.TopLeft, com.BoxSize) * user.Scale;
            if (clipToCanvas)
                rect = canvas.AdjustWithinCanvas(rect);
            if (rect != null)
            {
                // Draw background box
                if (com.BoxColor != null)
                {
                    canvas.AddRectFilled(rect, (Color)com.BoxColor);
                }

                // Create an InvisibleButton so that we can select it without dragging the entire
                // window. Names are not unique... probably not an issue? These don't really do
                // anything.
                ImGui.SetCursorScreenPos(canvas.Origin + rect.TopLeft);
                ImGui.InvisibleButton($"OverlayBox button {componentIndex}", rect.Size);

                // Draw it. (Ensure "com.Render" is immediately after ImGui.InvisibleButton so that the
                // Render function can manipulate it if necessary.)
                ImGui.SetCursorScreenPos(canvas.Origin);
                com.Render(com == user.Selected);

                // Check if we're hovering over it. But don't draw the hovering rectangle right now
                // because we don't want to draw it on more than one OverlayBox.
                rect = com.BoxRectangle;
                if (isWindowHovered && rect.Contains(mousePos))
                {
                    hoveringComponent = com;
                }
            }

            componentIndex++;
        }

        // If the mouse is clicked while not focused on the window, do not allow the tile grid to be
        // modified through mouse dragging until the mouse is released and clicked again. Prevents
        // accidental tile grid modifications in some situations, ie. when clicking outside a
        // right-click popup menu.
        if (!isWindowHovered && ImGui.IsMouseDown(ImGuiMouseButton.Left) && !user.SelectingRectangle)
            user.SuppressCurrentClick();

        // Draw hover outline if something is being hovered over
        if (hoveringComponent != null)
        {
            canvas.AddRect(hoveringComponent.BoxRectangle * user.Scale, Color.Cyan, thickness: user.Scale);
        }
        // Draw selection outline if something is selected
        if (user.Selected != null)
        {
            canvas.AddRect(user.Selected.BoxRectangle * user.Scale, Color.White, thickness: user.Scale);
        }

        // Check if we clicked on the hoveringComponent
        if (hoveringComponent != null)
        {
            var rect = hoveringComponent.BoxRectangle;

            if (rect.Contains(mousePos))
            {
                // Left or right click
                if ((ImGui.IsMouseClicked(ImGuiMouseButton.Left) || ImGui.IsMouseClicked(ImGuiMouseButton.Right)) && !user.DraggingComponent)
                {
                    // Set dragOffset to non-null if we should start dragging it.
                    // In any case the hovered object will become the selected object.
                    Vector2? dragOffset = null;

                    if (allowMove && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        if (hoveringComponent.HasXY)
                            dragOffset = new Vector2(hoveringComponent.X, hoveringComponent.Y) - mousePos;
                        else
                            dragOffset = new Vector2(0, 0); // Just set it to anything non-null
                    }
                    user.SetSelected(hoveringComponent, dragOffset != null, dragOffset);
                }

                // Right click
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right) && !user.DraggingComponent)
                {
                    ImGui.OpenPopup(Top.RightClickPopupName);
                }

                // Double-click
                if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                {
                    hoveringComponent.OnDoubleClicked();
                }
            }
        }

        // Update room component dragging
        if (!allowMove)
        {
            user.DraggingComponent = false;
        }
        else if (user.DraggingComponent)
        {
            var com = user.Selected;

            if (com != null && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                project?.BeginTransaction("Move " + com.GetTransactionIdentifier(), true);

                // No XY: Can't move this at all by dragging.
                if (!com.HasXY)
                {
                }
                // Shortened XY: Snapping is "automatic" due to low resolution of XY variables;
                // also don't add draggingComponentOffset as it only works well with smooth
                // movement
                else if (com.HasShortenedXY)
                {
                    com.X = (int)Math.Round(mousePos.X);
                    com.Y = (int)Math.Round(mousePos.Y);
                }
                // Ctrl pressed: No snapping
                else if (alwaysFullPrecision || ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                {
                    com.X = (int)Math.Round(mousePos.X + user.DraggingComponentOffset.X);
                    com.Y = (int)Math.Round(mousePos.Y + user.DraggingComponentOffset.Y);
                }
                // Ctrl not pressed: Snap to 8x8
                else
                {
                    const int snap = 8;

                    var snapTo = (int oldPos, int mousePos) =>
                    {
                        int snapLog = (int)Math.Log(snap, 2);
                        int data = oldPos + snap / 2;
                        int align = data % snap;
                        int newp = (mousePos - align) >> snapLog;
                        newp = newp * snap + align + snap / 2;
                        return newp;
                    };

                    com.X = snapTo(com.X, (int)Math.Round(mousePos.X));
                    com.Y = snapTo(com.Y, (int)Math.Round(mousePos.Y));
                }

                project?.EndTransaction();
            }
            else
            {
                // Released the component
                project?.TransactionManager.InsertBarrier();
                user.DraggingComponent = false;
            }
        }

        if (ImGui.IsKeyPressed(ImGuiKey.Delete))
        {
            if (user.Selected != null
                && !(user.Selected is RoomLayoutEditor.AnnotationOverlayBox) // Delete key interferes with textbox
                && user.Selected.Deletable)
                user.Selected.Delete();
        }

        return hoveringComponent;
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}
