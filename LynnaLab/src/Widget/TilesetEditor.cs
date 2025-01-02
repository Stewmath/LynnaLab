namespace LynnaLab;

public class TilesetEditor : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetEditor(ProjectWorkspace workspace, string name)
        : base(name)
    {
        base.DefaultSize = new Vector2(1050, 900);
        this.Workspace = workspace;

        tilesetViewer = new TilesetViewer(Workspace);
        subTileViewer = new SubTileViewer(this);
        tileEditor = new TileEditor(this);
        subtileBrush = new Brush<SubTile>(new SubTile());

        tilesetViewer.InChildWindow = true;
        tilesetViewer.MinScale = 2.0f;
        tilesetViewer.MaxScale = 3.0f;
        tilesetViewer.Scale = 2.0f;
        tilesetViewer.ViewportSize = tilesetViewer.CanvasSize;

        tilesetViewer.SelectedEvent += (selectedIndex) =>
        {
            if (BrushMode == BrushMode.Normal && selectedIndex != -1)
                tileEditor.SetTile(Tileset, selectedIndex);
        };

        tilesetViewer.AfterRenderTileGrid += (_, _) =>
        {
            // In collision brush mode only, draw red on all solid tiles
            if (BrushMode != BrushMode.Collision)
                return;

            for (int tile=0; tile<tilesetViewer.MaxIndex; tile++)
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tile);
                DrawTileCollisions(tilesetViewer, Tileset, tile, t, tx, ty);
            }
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

    // For palette brush mode
    int selectedPalette; // Palette to set

    // For subtile brush mode
    bool copyPalettes = true; // Whether to copy palettes
    Brush<SubTile> subtileBrush; // Brush to use
    BrushInterfacer subtileBrushInterfacer;

    // ================================================================================
    // Properties
    // ================================================================================
    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }
    public RealTileset Tileset { get; private set; }
    public BrushMode BrushMode { get; private set;  }
    public bool EditAllSeasons { get; private set; } = true; // Whether modifying one season should affect other seasons

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

        if (Project.Game == Game.Seasons)
        {
            ImGui.SameLine();
            ImGuiX.Checkbox("Edit all seasons", new Accessor<bool>(() => EditAllSeasons));
            ImGuiX.TooltipOnHover("Changes to tiles affect all seasons, not just the current selected one.");
        }

        ImGui.PushItemWidth(200.0f);
        if (ImGui.BeginCombo("##Brush Mode", BrushModeString(BrushMode)))
        {
            string[] tooltips = {
                "Select from Tileset (left), drag subtiles from Subtile viewer (top-right) to Tile preview (bottom-right)",
                "Draw palette assignments directly onto the tileset",
                "Draw subtiles directly onto the tileset; select by right-clicking on the tileset or subtile viewer",
                "Draw collision data directly onto the tileset (left-click to set, right-click to clear)",
            };

            foreach (BrushMode mode in new BrushMode[] { BrushMode.Normal, BrushMode.Palette, BrushMode.Subtile, BrushMode.Collision })
            {
                if (ImGui.Selectable(BrushModeString(mode)))
                {
                    BrushMode = mode;
                    tilesetViewer.Selectable = mode == BrushMode.Normal;
                    tilesetViewer.SubTileMode = mode != BrushMode.Normal;
                    tilesetViewer.BrushInterfacer = mode == BrushMode.Subtile ? subtileBrushInterfacer : null;
                    tilesetViewer.TooltipImagePreview = mode != BrushMode.Subtile || subtileBrush.IsSingleTile;
                    RegisterMouseActions();
                }
                if (mode == BrushMode)
                    ImGui.SetItemDefaultFocus();
                if (ImGui.IsItemHovered())
                    ImGuiX.Tooltip(tooltips[(int)mode]);
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();

        if (BrushMode == BrushMode.Palette)
        {
            ImGui.PushItemWidth(ImGuiLL.ENTRY_ITEM_WIDTH);
            ImGui.SameLine();
            ImGuiX.InputHex("Palette index", ref selectedPalette, digits: 1, max: 7);
            ImGui.PopItemWidth();
        }
        else if (BrushMode == BrushMode.Subtile)
        {
            ImGui.SameLine();
            ImGui.Checkbox("Copy palettes", ref copyPalettes);
            ImGuiX.TooltipOnHover("Whether to copy palettes from the selection or leave palettes unchanged when drawing.");
        }

        ImGui.BeginChild("Tileset Viewer Panel", new Vector2(tilesetViewer.WidgetSize.X, HEIGHT));
        ImGui.SeparatorText("Tileset");
        tilesetViewer.Render();
        if (tilesetViewer.SubTileMode && tilesetViewer.HoveredTile != -1)
        {
            // Change flip X
            if (ImGui.IsKeyPressed(ImGuiKey._1))
            {
                Project.BeginTransaction("Toggle flip X");
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                byte flags = Tileset.GetSubTileFlags(t, tx, ty);
                flags ^= 0x20;
                ForAllSeasons((tileset) => tileset.SetSubTileFlags(t, tx, ty, flags));
                Project.EndTransaction();
            }
            // Change flip Y
            if (ImGui.IsKeyPressed(ImGuiKey._2))
            {
                Project.BeginTransaction("Toggle flip Y");
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                byte flags = Tileset.GetSubTileFlags(t, tx, ty);
                flags ^= 0x40;
                ForAllSeasons((tileset) => tileset.SetSubTileFlags(t, tx, ty, flags));
                Project.EndTransaction();
            }
            // Change priority
            if (ImGui.IsKeyPressed(ImGuiKey._3))
            {
                Project.BeginTransaction("Toggle priority");
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                byte flags = Tileset.GetSubTileFlags(t, tx, ty);
                flags ^= 0x80;
                ForAllSeasons((tileset) => tileset.SetSubTileFlags(t, tx, ty, flags));
                Project.EndTransaction();
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("SubTile Panel", new Vector2(subTileViewer.WidgetSize.X, HEIGHT));
        ImGui.SeparatorText($"SubTiles");
        subTileViewer.Render();
        ImGui.SeparatorText($"Tile {tileEditor.TileIndex:X2}");
        tileEditor.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Palette Panel", new Vector2(0.0f, HEIGHT));
        ImGuiLL.RenderPaletteHeader(Tileset.PaletteHeaderGroup, BrushMode == BrushMode.Palette ? selectedPalette : -1, Workspace);
        ImGui.EndChild();

        if (ImGui.CollapsingHeader("Tileset Properties"))
        {
            ImGuiLL.RenderTilesetFields(Tileset, Workspace.ShowDocumentation);
        }
    }

    public void SetTileset(int index, int season)
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

    public void SetTileset(RealTileset t)
    {
        if (t == Tileset)
            return;
        Tileset = t;
        tilesetViewer.SetTileset(Tileset);
        subTileViewer.SetTileset(Tileset);
        if (BrushMode == BrushMode.Normal)
            tileEditor.SetTile(Tileset, tilesetViewer.SelectedIndex);
        else
            tileEditor.SetTile(Tileset, 0);
        UpdateSubTilePreviewImage();
    }

    /// <summary>
    /// Do an action on either the current selected tilesets, or on all seasonal variants of the
    /// tileset, if "editAllSeasons" is true.
    /// </summary>
    public void ForAllSeasons(Action<RealTileset> action)
    {
        if (EditAllSeasons && Tileset.IsSeasonal)
        {
            for (int s=0; s<4; s++)
            {
                RealTileset t = Project.GetTileset(Tileset.Index, s);
                action(t);
            }
        }
        else
            action(Tileset);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Called when drawing on position (x,y) in subtile brush mode
    /// </summary>
    void SubTileDrawer(int x, int y, SubTile subTile)
    {
        if (!tilesetViewer.XYValid(x, y))
            return;

        Project.BeginTransaction($"Draw subtiles#{Tileset.TransactionIdentifier}", true);

        ForAllSeasons((tileset) =>
        {
            var (t, tx, ty) = tilesetViewer.ToSubTileIndex(x + y * tilesetViewer.Width);
            tileset.SetSubTileIndex(t, tx, ty, (byte)subTile.subtile);
            if (subTile.palette == -1 || !copyPalettes)
                tileset.SetSubTileFlags(t, tx, ty, subTile.GetFlags(tileset.GetSubTilePalette(t, tx, ty)));
            else
                tileset.SetSubTileFlags(t, tx, ty, subTile.GetFlags());
        });

        Project.EndTransaction();
    }

    void RegisterMouseActions()
    {
        // The mouse actions we register in the tilesetViewer are only enabled situationally. This
        // function unregisters everything and then reregisters only what we need for the current mode.
        tilesetViewer.RemoveMouseAction("Brush LeftClick");
        tilesetViewer.RemoveMouseAction("Brush Ctrl+LeftClick");
        tilesetViewer.RemoveMouseAction("Palette Brush RightClick");
        tilesetViewer.RemoveMouseAction("Subtile Brush RightClick");
        tilesetViewer.RemoveMouseAction("Collision Brush RightClick");
        tilesetViewer.RemoveMouseAction("Collision Brush Ctrl+RightClick");

        subTileViewer.RemoveMouseAction("Subtile Viewer RightClick");

        // Helper function for palette brush mode
        var paletteSetter = (TileGridEventArgs args) =>
        {
            Project.BeginTransaction($"Draw palettes#{Tileset.TransactionIdentifier}", true);

            ForAllSeasons((tileset) =>
            {
                args.Foreach((tile) =>
                {
                    var (t, x, y) = tilesetViewer.ToSubTileIndex(tile);
                    tileset.SetSubTilePalette(t, x, y, selectedPalette);
                });
            });

            Project.EndTransaction();
        };

        // Helper function for collision brush mode
        var collisionSetter = (TileGridEventArgs args, bool enable) =>
        {
            Project.BeginTransaction($"Draw collisions#{Tileset.TransactionIdentifier}", true);

            ForAllSeasons((tileset) =>
            {
                args.Foreach((tile) =>
                {
                    var (t, x, y) = tilesetViewer.ToSubTileIndex(tile);
                    TrySetSubTileCollision(tileset, t, x, y, enable);
                });
            });

            Project.EndTransaction();
        };

        if (BrushMode != BrushMode.Normal)
        {
            // Left click: Draw when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    var (t, x, y) = tilesetViewer.ToSubTileIndex(args.selectedIndex);

                    if (BrushMode == BrushMode.Palette)
                    {
                        paletteSetter(args);
                    }
                    else if (BrushMode == BrushMode.Subtile)
                    {
                        subtileBrush.Draw(SubTileDrawer,
                                          args.selectedIndex % tilesetViewer.Width,
                                          args.selectedIndex / tilesetViewer.Width,
                                          subtileBrush.BrushWidth,
                                          subtileBrush.BrushHeight);
                    }
                    else if (BrushMode == BrushMode.Collision)
                    {
                        collisionSetter(args, true);
                    }

                    tileEditor.SetTile(Tileset, t);
                },
                name: "Brush LeftClick",
                onRelease: Project.UndoState.InsertBarrier
            );

            // Ctrl + left click: Rectangle fill when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    if (BrushMode == BrushMode.Palette)
                    {
                        paletteSetter(args);
                    }
                    else if (BrushMode == BrushMode.Subtile)
                    {
                        subtileBrush.Draw(SubTileDrawer,
                                          args.topLeft.X,
                                          args.topLeft.Y,
                                          args.SelectedWidth,
                                          args.SelectedHeight
                        );
                    }
                    else if (BrushMode == BrushMode.Collision)
                    {
                        collisionSetter(args, true);
                    }
                },
                brushPreview: true,
                name: "Brush Ctrl+LeftClick",
                onRelease: Project.UndoState.InsertBarrier
            );
        }

        if (BrushMode == BrushMode.Palette)
        {
            // Right click: Copy when in palette brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.Click, GridAction.Callback,
                (_, args) =>
                {
                    if (BrushMode == BrushMode.Palette)
                    {
                        var (t, x, y) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                        selectedPalette = Tileset.GetSubTilePalette(t, x, y);
                        tileEditor.SetTile(Tileset, t);
                    }
                },
                name: "Palette Brush RightClick"
            );
        }

        if (BrushMode == BrushMode.Subtile)
        {
            // Right click + drag: Rectangle select when in subtile brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    if (BrushMode == BrushMode.Subtile)
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

        if (BrushMode == BrushMode.Collision)
        {
            // Right click: Unset collision when in collision brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    collisionSetter(args, false);
                    var (t, _, _) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                    tileEditor.SetTile(Tileset, t);
                },
                name: "Collision Brush RightClick",
                onRelease: Project.UndoState.InsertBarrier
            );
            // Right click + drag: Unset collision range when in collision brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    collisionSetter(args, false);
                },
                name: "Collision Brush Ctrl+RightClick",
                onRelease: Project.UndoState.InsertBarrier
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
    // Static methods
    // ================================================================================

    /// <summary>
    /// Draws the transparect rectangles representing a tile's solidity in collision brush mode.
    /// </summary>
    public static void DrawTileCollisions(TileGrid grid, Tileset tileset, int drawTilePos, int t, int tx, int ty)
    {
        if (tileset.GetTileCollision(t) >= 0x10)
        {
            // Special collision mode: Blue rectangle (cannot be modified by drawing on it)
            grid.AddRectFilled(grid.TileRect(drawTilePos), Color.FromRgbaDbl(0, 0, 1.0f, 0.5f));
        }
        else if (tileset.GetSubTileBasicCollision(t, tx, ty))
        {
            // Solid tile
            grid.AddRectFilled(grid.TileRect(drawTilePos), Color.FromRgbaDbl(1.0f, 0, 0, 0.5f));
        }
    }

    public static void TrySetSubTileCollision(Tileset tileset, int t, int tx, int ty, bool enabled)
    {
        if (tileset.GetTileCollision(t) >= 0x10)
            return;
        tileset.SetSubTileBasicCollision(t, tx, ty, enabled);
    }

    // ================================================================================
    // Nested structs/enums
    // ================================================================================

    static string BrushModeString(BrushMode brushMode)
    {
        switch (brushMode)
        {
            case BrushMode.Normal: return "Selection Mode";
            case BrushMode.Palette: return "Palette Brush Mode";
            case BrushMode.Subtile: return "Subtile Brush Mode";
            case BrushMode.Collision: return "Collision Brush Mode";
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

public enum BrushMode
{
    Normal = 0,
    Palette = 1,
    Subtile = 2,
    Collision = 3,
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

        base.OnDrop = () =>
        {
            int? value = ImGuiX.AcceptDragDropPayload<int>("SubTile Index");
            if (value == null)
                return;
            int tile = CoordToTile(GetRelativeMousePos());
            int x = tile % 2;
            int y = tile / 2;

            Project.BeginTransaction("Set subtile");

            ForAllSeasons((tileset) =>
            {
                tileset.SetSubTileIndex(TileIndex, x, y, (byte)(value ^ 0x80));
            });

            Project.EndTransaction();
        };

        var trySetCollision = (TileGridEventArgs args, bool enable) =>
        {
            if (parent.BrushMode != BrushMode.Collision)
                return;

            Project.BeginTransaction($"Draw collisions#{Tileset.TransactionIdentifier}-t{TileIndex:X2}", true);

            ForAllSeasons((tileset) =>
            {
                args.Foreach((index) =>
                {
                    int t = TileIndex;
                    int tx = index % 2;
                    int ty = index / 2;
                    TilesetEditor.TrySetSubTileCollision(tileset, t, tx, ty, enable);
                });
            });

            Project.EndTransaction();
        };

        // Register mouse actions for setting collisions with the mouse
        base.AddMouseAction(
            MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
            (_, args) => trySetCollision(args, true));
        base.AddMouseAction(
            MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (_, args) => trySetCollision(args, true));
        base.AddMouseAction(
            MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
            (_, args) => trySetCollision(args, false));
        base.AddMouseAction(
            MouseButton.RightClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
            (_, args) => trySetCollision(args, false));

        base.AfterRenderTileGrid += (_, _) =>
        {
            // In collision brush mode only, draw red on all solid tiles
            if (parent.BrushMode != BrushMode.Collision)
                return;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    TilesetEditor.DrawTileCollisions(this, Tileset, x + y*2, TileIndex, x, y);
                }
            }
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

    public Project Project { get { return parent.Project; } }
    public RealTileset Tileset { get; private set; }
    public int TileIndex { get; private set; } // Should never be -1

    public override Image Image { get { return image; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        base.Selectable = parent.BrushMode != BrushMode.Collision;

        ImGui.BeginGroup();
        base.Render();

        // Show tile values while hovered or selected
        int previewIndex = SelectedIndex;
        if (ImGui.IsItemHovered())
            previewIndex = CoordToTile(GetRelativeMousePos());

        // Get an estimate of excess horizontal space to help with roughly centering the input fields
        float freeSpace = ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize("Subtile: FF").X;
        freeSpace = Math.Max(freeSpace, 0.0f);
        ImGuiX.ShiftCursorScreenPos(freeSpace / 2, 0.0f);
        ImGui.BeginChild("Text fields");

        if (parent.BrushMode == BrushMode.Collision)
        {
            // In collision brush mode, instead of rendering subtile properties, just show the
            // collision data for this tile.
            ImGui.PushItemWidth(100.0f);
            ImGui.Text("Collision:");
            ImGuiX.InputHex("##Collision", Tileset.GetTileCollision(TileIndex), (c) =>
            {
                Project.BeginTransaction($"Set collision#{Tileset.TransactionIdentifier}-t{TileIndex:X2}", true);
                ForAllSeasons((tileset) => tileset.SetTileCollision(TileIndex, (byte)c));
                Project.EndTransaction();
            });
            ImGui.PopItemWidth();
        }
        else if (previewIndex != -1)
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

                    Project.BeginTransaction("Toggle " + name);
                    ForAllSeasons((tileset) => tileset.SetSubTileFlags(TileIndex, x, y, newFlags));
                    Project.EndTransaction();
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
        ImGui.EndChild();
        ImGui.EndGroup();
    }

    public void SetTile(RealTileset t, int index)
    {
        Debug.Assert(index != -1);
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
        image = TopLevel.ImageFromBitmapTracked(Tileset.GetTileBitmap(TileIndex));
    }

    /// <summary>
    /// Just like the ForAllSeasons function in TileEditor class
    /// </summary>
    void ForAllSeasons(Action<RealTileset> action)
    {
        parent.ForAllSeasons(action);
    }
}
