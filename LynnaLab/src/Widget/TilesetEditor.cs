using System.Diagnostics;

namespace LynnaLab;

public class TilesetEditor : Frame
{
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
        subtileBrush = new Brush<SubTile>(new SubTile());

        tilesetViewer.InChildWindow = true;
        tilesetViewer.MaxScale = 3.0f;
        tilesetViewer.ViewportSize = tilesetViewer.CanvasSize;
        tilesetViewer.SelectedEvent += (selectedIndex) =>
        {
            if (brushMode == BrushMode.Normal)
                tileEditor.SetTile(Tileset, selectedIndex);
        };

        subtileBrushInterfacer = BrushInterfacer.Create(subtileBrush);

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
    Brush<SubTile> subtileBrush; // Brush to use when in subtile brush mode
    BrushInterfacer subtileBrushInterfacer;

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

        ImGui.PushItemWidth(200.0f);
        if (ImGui.BeginCombo("##Brush Mode", BrushModeString(brushMode)))
        {
            string[] tooltips = {
                "Select from Tileset, drag subtiles from Subtile viewer to Tile viewer",
                "Draw palette assignments directly onto the tileset",
                "Draw subtiles directly onto the tileset; select by right-clicking on the tileset viewer or subtile viewer"
            };

            foreach (BrushMode mode in new BrushMode[] { BrushMode.Normal, BrushMode.Palette, BrushMode.Subtile })
            {
                if (ImGui.Selectable(BrushModeString(mode)))
                {
                    brushMode = mode;
                    tilesetViewer.Selectable = mode == BrushMode.Normal;
                    tilesetViewer.SubTileMode = mode != BrushMode.Normal;
                    tilesetViewer.BrushInterfacer = mode == BrushMode.Subtile ? subtileBrushInterfacer : null;
                    tilesetViewer.TooltipImagePreview = mode != BrushMode.Subtile || subtileBrush.IsSingleTile;
                    RegisterMouseActions();
                }
                if (mode == brushMode)
                    ImGui.SetItemDefaultFocus();
                if (ImGui.IsItemHovered())
                    ImGuiX.Tooltip(tooltips[(int)mode]);
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();

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
        if (tilesetViewer.SubTileMode && tilesetViewer.HoveredTile != -1)
        {
            // Change flip X
            if (ImGui.IsKeyPressed(ImGuiKey._1))
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                byte flags = Tileset.GetSubTileFlags(t, tx, ty);
                flags ^= 0x20;
                Tileset.SetSubTileFlags(t, tx, ty, flags);
            }
            // Change flip Y
            if (ImGui.IsKeyPressed(ImGuiKey._2))
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                byte flags = Tileset.GetSubTileFlags(t, tx, ty);
                flags ^= 0x40;
                Tileset.SetSubTileFlags(t, tx, ty, flags);
            }
            // Change priority
            if (ImGui.IsKeyPressed(ImGuiKey._3))
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                byte flags = Tileset.GetSubTileFlags(t, tx, ty);
                flags ^= 0x80;
                Tileset.SetSubTileFlags(t, tx, ty, flags);
            }
        }
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
        if (brushMode == BrushMode.Normal)
            tileEditor.SetTile(Tileset, tilesetViewer.SelectedIndex);
        else
            tileEditor.SetTile(Tileset, 0);
        UpdateSubTilePreviewImage();
    }

    /// <summary>
    /// Called when drawing on position (x,y) in subtile brush mode
    /// </summary>
    void SubTileDrawer(int x, int y, SubTile subTile)
    {
        if (!tilesetViewer.XYValid(x, y))
            return;
        var (t, tx, ty) = tilesetViewer.ToSubTileIndex(x + y * tilesetViewer.Width);
        Tileset.SetSubTileIndex(t, tx, ty, (byte)subTile.subtile);
        if (subTile.palette == -1)
            Tileset.SetSubTileFlags(t, tx, ty, subTile.GetFlags(Tileset.GetSubTilePalette(t, tx, ty)));
        else
            Tileset.SetSubTileFlags(t, tx, ty, subTile.GetFlags());
    }

    void RegisterMouseActions()
    {
        // The mouse actions we register in the tilesetViewer are only enabled situationally. This
        // function unregisters everything and then reregisters only what we need for the current mode.
        tilesetViewer.RemoveMouseAction("Brush LeftClick");
        tilesetViewer.RemoveMouseAction("Brush Ctrl+LeftClick");
        tilesetViewer.RemoveMouseAction("Palette Brush RightClick");
        tilesetViewer.RemoveMouseAction("Subtile Brush RightClick");

        subTileViewer.RemoveMouseAction("Subtile Viewer RightClick");

        if (brushMode != BrushMode.Normal)
        {
            // Left click: Draw when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    var (t, x, y) = tilesetViewer.ToSubTileIndex(args.selectedIndex);

                    if (brushMode == BrushMode.Palette)
                    {
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

                    tileEditor.SetTile(Tileset, t);
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
                brushPreview: true,
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
                        tileEditor.SetTile(Tileset, t);
                    }
                },
                name: "Palette Brush RightClick"
            );
        }

        if (brushMode == BrushMode.Subtile)
        {
            // Right click + drag: Rectangle select when in subtile brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    if (brushMode == BrushMode.Subtile)
                    {
                        subtileBrush.SetTiles(tilesetViewer, args.RectArray((x, y) =>
                        {
                            var (t, tx, ty) = tilesetViewer.ToSubTileIndex(x + y * tilesetViewer.Width);
                            byte flags = tilesetViewer.Tileset.GetSubTileFlags(t, tx, ty);
                            return new SubTile{
                                subtile = tilesetViewer.Tileset.GetSubTileIndex(t, tx, ty),
                                palette = tilesetViewer.Tileset.GetSubTilePalette(t, tx, ty),
                                flipX = (flags & 0x20) != 0,
                                flipY = (flags & 0x40) != 0,
                                priority = (flags & 0x80) != 0,
                                };
                        }));

                        UpdateSubTilePreviewImage();

                        // If we selected just one tile, select it in the tile editor widget
                        if (subtileBrush.IsSingleTile)
                        {
                            var (t, _, _) = tilesetViewer.ToSubTileIndex(args.topLeft.X + args.topLeft.Y * tilesetViewer.Width);
                            tileEditor.SetTile(Tileset, t);
                        }
                    }
                },
                name: "Subtile Brush RightClick"
            );

            // Subtile viewer: Right click + drag to select tiles (subtile brush mode only)
            subTileViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    subtileBrush.SetTiles(tileEditor, args.RectArray((x, y) =>
                    {
                        return new SubTile {
                            subtile = (x + y * subTileViewer.Width) ^ 0x80,
                            palette = -1, // Use pre-existing palettes when drawn
                            flipX = false,
                            flipY = false,
                            priority = false,
                        };
                    }));

                    UpdateSubTilePreviewImage();
                },
                name: "Subtile Viewer RightClick"
            );
        }
    }

    /// <summary>
    /// Render the preview image for the subtileBrush, typically called after the brush is updated.
    /// </summary>
    void UpdateSubTilePreviewImage()
    {
        using (Bitmap bitmap = new Bitmap(subtileBrush.BrushWidth * 8, subtileBrush.BrushHeight * 8))
        {
            bitmap.Lock();
            subtileBrush.Draw(
                (x, y, subtile) =>
                {
                    Color[] palette;
                    if (subtile.palette == -1)
                        palette = GbGraphics.GrayPalette;
                    else
                        palette = Tileset.GraphicsState.GetBackgroundPalettes()[subtile.palette & 7];

                    byte flags = subtile.GetFlags(0);

                    GbGraphics.RenderRawTile(bitmap, x * 8, y * 8,
                                             Tileset.GetSubTileGfxBytes(subtile.subtile),
                                             palette,
                                             flags);
                },
                0, 0, subtileBrush.BrushWidth, subtileBrush.BrushHeight
            );
            bitmap.Unlock();
            Image image = TopLevel.Backend.ImageFromBitmap(bitmap);
            subtileBrushInterfacer.SetPreviewImage(image, 8);
            tilesetViewer.TooltipImagePreview = subtileBrush.IsSingleTile;
        }
    }

    // ================================================================================
    // Nested structs/enums
    // ================================================================================

    enum BrushMode
    {
        Normal = 0,
        Palette = 1,
        Subtile = 2,
    }

    static string BrushModeString(BrushMode brushMode)
    {
        switch (brushMode)
        {
            case BrushMode.Normal: return "Selection Mode";
            case BrushMode.Palette: return "Palette Brush Mode";
            case BrushMode.Subtile: return "Subtile Brush Mode";
            default: throw new Exception();
        }
    }

    /// <summary>
    /// Struct used as the "tile" template class in "Brush" containing all the info that we want to
    /// be able to copy from one subtile to another. So, not just the tile index itself but also the
    /// palette, etc.
    /// </summary>
    struct SubTile
    {
        public int subtile, palette; // palette can be -1 for "undetermined"
        public bool flipX, flipY, priority;

        public byte GetFlags(int paletteOverride = -1)
        {
            Debug.Assert(paletteOverride >= -1 && paletteOverride <= 7);
            if (paletteOverride == -1)
                paletteOverride = palette;
            if (paletteOverride == -1)
                throw new Exception("SubTile.GetFlags(): Palette undefined");
            return (byte)(paletteOverride | 0x08 | (flipX ? 0x20 : 0) | (flipY ? 0x40 : 0) | (priority ? 0x80 : 0));
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
        base.TooltipImagePreview = true;
        base.TooltipImagePreviewScale = TILE_IMAGE_SCALE;

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
