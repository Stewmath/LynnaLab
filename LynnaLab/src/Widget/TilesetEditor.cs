namespace LynnaLab;

public class TilesetEditor : Frame
{
    enum BrushMode
    {
        Normal = 0,
        Palette = 1,
        Subtile = 2,
    }

    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetEditor(ProjectWorkspace workspace, string name)
        : base(name)
    {
        Workspace = workspace;
        tilesetViewer = new TilesetViewer(Workspace);
        subTileViewer = new SubTileViewer(this);
        tileEditor = new TileEditor(this);
        subtileBrush = new Brush();

        tilesetViewer.SelectedEvent += (selectedIndex) =>
        {
            if (brushMode == BrushMode.Normal)
                tileEditor.SetTile(Tileset, selectedIndex);
        };

        RegisterMouseActions();

        SetTileset(0, 1);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    TilesetViewer tilesetViewer;
    SubTileViewer subTileViewer;
    TileEditor tileEditor;

    BrushMode brushMode;
    int selectedPalette; // Palette to set when in palette brush mode
    Brush subtileBrush; // Brush to use when in subtile brush mode

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public RealTileset Tileset { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        float HEIGHT = tilesetViewer.WidgetSize.Y + 40.0f;

        ImGuiLL.TilesetChooser(Project, "Chooser", Tileset.Index, Tileset.Season,
            (t, s) => SetTileset(t, s));
        ImGui.SameLine();
        ImGuiX.ShiftCursorScreenPos(10.0f, 0.0f);

        if (ImGui.Button("Open Cloner"))
        {
            Workspace.OpenTilesetCloner(Tileset, Tileset);
        }

        ImGuiX.Checkbox("Palette brush mode", brushMode == BrushMode.Palette, (brushOn) =>
        {
            brushMode = brushOn ? BrushMode.Palette : BrushMode.Normal;
            tilesetViewer.SubTileMode = brushOn;
            tilesetViewer.Selectable = !brushOn;
            RegisterMouseActions();
        });
        ImGui.SameLine();
        ImGuiX.Checkbox("Subtile brush mode", brushMode == BrushMode.Subtile, (brushOn) =>
        {
            brushMode = brushOn ? BrushMode.Subtile : BrushMode.Normal;
            tilesetViewer.SubTileMode = brushOn;
            tilesetViewer.Selectable = !brushOn;
            RegisterMouseActions();
        });

        if (brushMode == BrushMode.Palette)
        {
            ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
            ImGui.SameLine();
            ImGuiX.InputHex("Palette index", ref selectedPalette, digits: 1, max: 7);
            ImGui.PopItemWidth();
        }

        ImGui.BeginChild("Tileset Viewer Panel", new Vector2(tilesetViewer.WidgetSize.X, HEIGHT));
        ImGui.SeparatorText("Tileset");
        tilesetViewer.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Tile Editor Panel", new Vector2(100.0f, HEIGHT));
        ImGui.SeparatorText($"Tile {tileEditor.TileIndex:X2}");
        tileEditor.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("SubTile Panel", new Vector2(subTileViewer.WidgetSize.X, HEIGHT));
        ImGui.SeparatorText($"SubTiles");
        subTileViewer.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Palette Panel", new Vector2(0.0f, HEIGHT));
        ImGuiLL.RenderPaletteHeader(Tileset.PaletteHeaderGroup, brushMode == BrushMode.Palette ? selectedPalette : -1);
        ImGui.EndChild();

        ImGui.SeparatorText("Tileset Properties");
        ImGuiLL.RenderTilesetFields(Tileset, Workspace.ShowDocumentation);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void SetTileset(int index, int season)
    {
        if (Tileset != null && Tileset.Index == index && Tileset.Season == season)
            return;

        bool isSeasonal = Project.TilesetIsSeasonal(index);

        if (isSeasonal && (season < 0 || season > 3))
            season = 0;
        if (!isSeasonal && season != -1)
            season = -1;

        SetTileset(Project.GetTileset(index, season));
    }

    void SetTileset(RealTileset t)
    {
        if (t == Tileset)
            return;
        Tileset = t;
        tilesetViewer.SetTileset(Tileset);
        subTileViewer.SetTileset(Tileset);
        tileEditor.SetTile(Tileset, tilesetViewer.SelectedIndex);
    }

    void SubTileDrawer(int x, int y, int tile)
    {
        if (!tilesetViewer.XYValid(x, y))
            return;
        var (t, tx, ty) = tilesetViewer.ToSubTileIndex(x + y * tilesetViewer.Width);
        Tileset.SetSubTileIndex(t, tx, ty, (byte)tile);
    }

    void RegisterMouseActions()
    {
        // The mouse actions we register in the tilesetViewer are only enabled situationally. This
        // function unregisters everything and then reregisters only what we need for the current mode.
        tilesetViewer.RemoveMouseAction("Brush LeftClick");
        tilesetViewer.RemoveMouseAction("Brush Ctrl+LeftClick");
        tilesetViewer.RemoveMouseAction("Palette Brush RightClick");
        tilesetViewer.RemoveMouseAction("Subtile Brush RightClick");

        if (brushMode != BrushMode.Normal)
        {
            // Left click: Draw when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    if (brushMode == BrushMode.Palette)
                    {
                        var (t, x, y) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                        Tileset.SetSubTilePalette(t, x, y, selectedPalette);
                    }
                    else if (brushMode == BrushMode.Subtile)
                    {
                        subtileBrush.Draw(SubTileDrawer,
                                          args.selectedIndex % tilesetViewer.Width,
                                          args.selectedIndex / tilesetViewer.Width,
                                          subtileBrush.BrushWidth,
                                          subtileBrush.BrushHeight);
                    }
                },
                name: "Brush LeftClick"
            );

            // Ctrl + left click: Rectangle fill when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    if (brushMode == BrushMode.Palette)
                    {
                        args.Foreach((x1, y1) =>
                        {
                            int tile = x1 + y1 * tilesetViewer.Width;
                            var (t, x, y) = tilesetViewer.ToSubTileIndex(tile);
                            Tileset.SetSubTilePalette(t, x, y, selectedPalette);
                        });
                    }
                    else if (brushMode == BrushMode.Subtile)
                    {
                        subtileBrush.Draw(SubTileDrawer,
                                          args.topLeft.X,
                                          args.topLeft.Y,
                                          args.SelectedWidth,
                                          args.SelectedHeight
                        );
                    }
                },
                name: "Brush Ctrl+LeftClick"
            );
        }

        if (brushMode == BrushMode.Palette)
        {
            // Right click: Copy when in palette brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.Click, GridAction.Callback,
                (_, args) =>
                {
                    if (brushMode == BrushMode.Palette)
                    {
                        var (t, x, y) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                        selectedPalette = Tileset.GetSubTilePalette(t, x, y);
                    }
                },
                name: "Palette Brush RightClick"
            );
        }

        if (brushMode == BrushMode.Subtile)
        {
            // Right click + drag: Rectangle select when in subtile brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick,
                MouseModifier.None,
                MouseAction.ClickDrag,
                GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    if (brushMode == BrushMode.Subtile)
                    {
                        subtileBrush.SetTiles(tilesetViewer, args.RectArray((x, y) =>
                        {
                            var (t, x2, y2) = tilesetViewer.ToSubTileIndex(x + y * tilesetViewer.Width);
                            return tilesetViewer.Tileset.GetSubTileIndex(t, x2, y2);
                        }));
                    }
                },
                name: "Subtile Brush RightClick"
            );
        }
    }
}

/// <summary>
/// Greyscale view of available subtiles
/// </summary>
class SubTileViewer : GfxViewer
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public SubTileViewer(TilesetEditor parent)
        : base(parent.Workspace, "SubTileViewer")
    {
        this.parent = parent;

        base.TileWidth = 8;
        base.TileHeight = 8;
        base.Width = 16;
        base.Height = 16;
        base.Scale = 2;
        base.Selectable = false;
        base.HoverImagePreview = true;
        base.HoverImagePreviewScale = TILE_IMAGE_SCALE;

        base.OnDrag = (index) =>
        {
            DragSubTile(index ^ 0x80);
        };

        base.OnHover = (index) =>
        {
            ImGui.BeginTooltip();
            ImGui.Text($"SubTile Index: {index^0x80:X2}");
            ImGui.EndTooltip();
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    TilesetEditor parent;

    const float TILE_IMAGE_SCALE = 6.0f;

    // ================================================================================
    // Properties
    // ================================================================================

    public Tileset Tileset { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        base.Render();
    }

    public void SetTileset(Tileset t)
    {
        if (Tileset == t)
            return;

        Tileset = t;

        base.SetGraphicsState(Tileset.GraphicsState, 0x2000, 0x3000);
    }

    // ================================================================================
    // Static methods
    // ================================================================================
    public static void DragSubTile(int index)
    {
        index ^= 0x80;
        ImGui.Text($"SubTile Index: {index:X2}");
        ImGuiX.SetDragDropPayload<int>("SubTile Index", index);
    }
}

/// <summary>
/// Zoomed in view of one 16x16 tile, consisting of 4 subtiles.
/// </summary>
class TileEditor : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public TileEditor(TilesetEditor parent)
        : base("Tile Editor")
    {
        this.parent = parent;

        base.TileWidth = base.TileHeight = 8;
        base.Width = base.Height = 2;
        base.Scale = 4;
        base.CenterX = true;

        base.Selectable = true;
        base.Unselectable = true;
        base.SelectedIndex = -1;

        tilesetEventWrapper = new EventWrapper<Tileset>();
        tilesetEventWrapper.Bind<int>("TileModifiedEvent", (_, tile) =>
        {
            if (tile == -1 || tile == TileIndex)
                DrawTile();
        });

        base.OnDrag = (index) =>
        {
            var (x, y) = TileToXY(index);
            SubTileViewer.DragSubTile(Tileset.GetSubTileIndex(TileIndex, x, y));
        };

        base.OnDrop = () =>
        {
            int? value = ImGuiX.AcceptDragDropPayload<int>("SubTile Index");
            if (value == null)
                return;
            int tile = CoordToTile(GetRelativeMousePos());
            int x = tile % 2;
            int y = tile / 2;
            Tileset.SetSubTileIndex(TileIndex, x, y, (byte)(value ^ 0x80));
        };
    }

    // ================================================================================
    // Variables
    // ================================================================================

    TilesetEditor parent;
    Image image;
    EventWrapper<Tileset> tilesetEventWrapper;

    // ================================================================================
    // Properties
    // ================================================================================

    public Tileset Tileset { get; private set; }
    public int TileIndex { get; private set; }

    protected override Image Image { get { return image; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        ImGui.BeginGroup();
        base.Render();

        // Show tile values while hovered or selected
        int previewIndex = SelectedIndex;
        if (ImGui.IsItemHovered())
            previewIndex = CoordToTile(GetRelativeMousePos());

        if (previewIndex != -1)
        {
            var (x, y) = TileToXY(previewIndex);
            byte flags = Tileset.GetSubTileFlags(TileIndex, x, y);

            var checkboxForBit = (string name, byte bitmask) =>
            {
                ImGuiX.Checkbox(name, (flags & bitmask) != 0, (v) =>
                {
                    byte newFlags = (byte)(flags & ~bitmask);
                    if (v)
                        newFlags |= bitmask;
                    Tileset.SetSubTileFlags(TileIndex, x, y, newFlags);
                });
            };

            ImGui.Text($"({x}, {y})");
            ImGui.Text($"Subtile: {Tileset.GetSubTileIndex(TileIndex, x, y):X2}");
            ImGui.Text($"Palette: {flags & 7}");
            checkboxForBit("Flip X", 0x20);
            checkboxForBit("Flip Y", 0x40);
            checkboxForBit("Priority", 0x80);

            if (ImGui.IsItemHovered())
            {
                ImGuiX.Tooltip("Tile is drawn above sprites when checked (except color 0, which is always drawn below sprites.)");
            }

            // Not showing "bank" bit (0x08) which should always be set
        }
        ImGui.EndGroup();
    }

    public void SetTile(Tileset t, int index)
    {
        Tileset = t;
        tilesetEventWrapper.ReplaceEventSource(Tileset);
        TileIndex = index;
        DrawTile();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void DrawTile()
    {
        image = TopLevel.ImageFromBitmap(Tileset.GetTileBitmap(TileIndex));
    }
}
