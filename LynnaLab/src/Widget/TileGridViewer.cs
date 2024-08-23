using System;
using System.Numerics;
using ImGuiNET;

using Point = Cairo.Point;
using Rect = Cairo.Rectangle;
using Color = LynnaLib.Color;
using System.Collections.Generic;

namespace LynnaLab
{
    /// <summary>
    /// Represents any kind of tile-based grid. It can be hovered over with the mouse, and
    /// optionally allows one to select tiles by clicking, or define actions to occur with other
    /// mouse buttons.
    /// </summary>
    public class TileGridViewer : Widget
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public TileGridViewer()
        {
            AddMouseAction(MouseButton.LeftClick, MouseModifier.Any, GridAction.Select);
        }

        // ================================================================================
        // Variables
        // ================================================================================
        List<TileGridAction> actionList = new List<TileGridAction>();

        // ================================================================================
        // Events
        // ================================================================================

        // ================================================================================
        // Properties
        // ================================================================================

        // Number of tiles on each axis
        public int Width { get; protected set; }
        public int Height { get; protected set; }

        // Size of tiles on each axis
        public int TileWidth { get; protected set; }
        public int TileHeight { get; protected set; }

        public int Scale { get; set; } = 1;

        public bool Selectable { get; set; }
        public int SelectedIndex { get; private set; }
        public int HoveringIndex
        {
            get
            {
                return CoordToTile(base.GetMousePos());
            }
        }

        public Color HoverColor { get; protected set; } = Color.Red;
        public Color SelectColor { get; protected set; } = Color.White;

        public int CanvasWidth
        {
            get
            {
                return TileWidth * Width * Scale;
            }
        }
        public int CanvasHeight
        {
            get
            {
                return TileHeight * Height * Scale;
            }
        }

        protected virtual Image Image { get { return null; } }

        // ================================================================================
        // Public methods
        // ================================================================================
        public override void Render()
        {
            base.Render();

            if (Image != null)
            {
                Widget.DrawImage(Image, scale: Scale);

                // Use InvisibleButton to prevent left click from dragging the window
                ImGui.SetCursorScreenPos(base.origin);
                ImGui.InvisibleButton("".AsSpan(), new Vector2(CanvasWidth, CanvasHeight));

                // Check mouse clicks
                int mouseIndex = CoordToTile(base.GetMousePos());

                if (ImGui.IsItemHovered() && mouseIndex != -1)
                {
                    TileGridEventArgs args = new TileGridEventArgs();
                    args.mouseAction = "click";
                    args.selectedIndex = mouseIndex;

                    foreach (TileGridAction action in actionList)
                    {
                        if (action.MatchesState())
                        {
                            if (action.action == GridAction.Callback)
                            {
                                action.callback(this, args);
                            }
                            else if (action.action == GridAction.Select)
                            {
                                if (Selectable)
                                    SelectedIndex = mouseIndex;
                            }
                            else
                                throw new NotImplementedException();
                        }
                    }
                }

                // Draw stuff on top

                if (ImGui.IsItemHovered())
                {
                    if (mouseIndex != -1)
                    {
                        Rect r = TileRect(mouseIndex);

                        base.AddRect(r, HoverColor, thickness: 2 * Scale);
                    }
                }

                if (Selectable && SelectedIndex != -1)
                {
                    Rect r = TileRect(SelectedIndex);
                    base.AddRect(r, SelectColor, thickness: 2 * Scale);
                }
            }
        }

        public void AddMouseAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null)
        {
            TileGridAction act;
            if (action == GridAction.Callback || action == GridAction.SelectRangeCallback)
            {
                if (callback == null)
                    throw new Exception("Need to specify a callback.");
                act = new TileGridAction(button, mod, action, callback);
            }
            else
            {
                if (callback != null)
                    throw new Exception("This action doesn't take a callback.");
                act = new TileGridAction(button, mod, action);
            }

            // Insert at front of list; newest actions get highest priority
            actionList.Insert(0, act);
        }


        // ================================================================================
        // Private methods
        // ================================================================================

        /// <summary>
        /// Converts a mouse position in pixels to a tile index.
        /// </summary>
        int CoordToTile(Point pos)
        {
            if (pos.X < 0 || pos.Y < 0 || pos.X >= CanvasWidth || pos.Y >= CanvasHeight)
                return -1;

            return (pos.Y / (TileHeight * Scale)) * Width + (pos.X / (TileWidth * Scale));
        }

        /// <summary>
        /// Gets the bounds of a tile in a rectangle.
        /// </summary>
        Rect TileRect(int tileIndex)
        {
            int x = tileIndex % Width;
            int y = tileIndex / Width;

            Point tl = new Point(x * TileWidth * Scale, y * TileHeight * Scale);
            return new Rect(tl.X, tl.Y, TileWidth * Scale, TileHeight * Scale);
        }


        // Nested class

        public delegate void TileGridEventHandler(object sender, TileGridEventArgs args);

        class TileGridAction
        {
            public readonly MouseButton button;
            public readonly MouseModifier mod;
            public readonly GridAction action;
            public readonly TileGridEventHandler callback;

            public TileGridAction(MouseButton button, MouseModifier mod, GridAction action, TileGridEventHandler callback = null)
            {
                this.button = button;
                this.mod = mod;
                this.action = action;
                this.callback = callback;
            }

            public bool MatchesState()
            {
                return ButtonMatchesState() && ModifierMatchesState();
            }

            public bool ButtonMatchesState()
            {
                Func<ImGuiMouseButton, bool> checker;
                if (mod.HasFlag(MouseModifier.Drag))
                    checker = ImGui.IsMouseDown;
                else
                    checker = ImGui.IsMouseClicked;
                bool left = checker(ImGuiMouseButton.Left);
                bool right = checker(ImGuiMouseButton.Right);
                if (button == MouseButton.Any && (left || right))
                    return true;
                if (button == MouseButton.LeftClick && left)
                    return true;
                if (button == MouseButton.RightClick && right)
                    return true;
                return false;
            }

            public bool ModifierMatchesState()
            {
                if (mod.HasFlag(MouseModifier.Any))
                    return true;
                MouseModifier flags = MouseModifier.None;
                if (ImGui.IsKeyDown(ImGuiKey.ModCtrl))
                    flags |= MouseModifier.Ctrl;
                if (ImGui.IsKeyDown(ImGuiKey.ModShift))
                    flags |= MouseModifier.Shift;

                return (mod & ~MouseModifier.Drag) == flags;
            }
        }
    }

    public enum MouseButton
    {
        Any,
        LeftClick,
        RightClick
    }

    [Flags]
    public enum MouseModifier
    {
        // Exact combination of keys must be pressed, but "Any" allows any combination.
        Any = 1,
        None = 2,
        Ctrl = 4 | None,
        Shift = 8 | None,
        // Mouse can be dragged during the operation.
        Drag = 16,
    }

    public enum GridAction
    {
        Select,       // Set the selected tile (bound to button "Any" with modifier "None" by default)
        Callback,     // Invoke a callback whenever the tile is clicked, or (optionally) the drag changes
        SelectRangeCallback,  // Select a range of tiles, takes a callback
    }

    public struct TileGridEventArgs
    {
        // "click", "drag", "release"
        public string mouseAction;

        // For GridAction.Callback (selected one tile)
        public int selectedIndex;

        // For GridAction.SelectRange
        public Cairo.Point topLeft, bottomRight;

        public void Foreach(System.Action<int, int> action)
        {
            for (int x=topLeft.X; x<=bottomRight.X; x++)
            {
                for (int y=topLeft.Y; y<=bottomRight.Y; y++)
                {
                    action(x, y);
                }
            }
        }
    }


}
