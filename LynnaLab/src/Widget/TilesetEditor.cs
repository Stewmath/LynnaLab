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
        tilesetViewer.RequestedScale = 2.0f;
        tilesetViewer.RequestedViewportSize = new Vector2(1, 1) * (256 * 2);

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

        OnBrushModeChanged(fromConstructor: true);

        SetTileset(0, Season.Summer);
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
    public BrushMode BrushMode { get; private set; } = BrushMode.Normal;
    public bool EditAllSeasons { get; private set; } = true; // Whether modifying one season should affect other seasons

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        float HEIGHT = tilesetViewer.WidgetSize.Y + ImGuiX.Unit(40.0f);

        ImGuiLL.TilesetChooser(Project, "Chooser", Tileset.Index, Tileset.Season,
            (t, s) => SetTileset(t, s));
        ImGui.SameLine();
        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(10.0f), 0.0f);

        if (ImGui.Button("Open Cloner"))
        {
            Workspace.OpenTilesetCloner(Tileset, Tileset);
        }

        ImGui.SameLine();
        if (ImGui.Button("Edit PNG"))
        {
            Workspace.OpenPNG(Tileset.GfxFileName);
        }

        if (Project.Game == Game.Seasons)
        {
            ImGui.SameLine();
            ImGuiX.Checkbox("Edit all seasons", new Accessor<bool>(() => EditAllSeasons));
            ImGuiX.TooltipOnHover("Changes to tiles affect all seasons, not just the current selected one.");
        }

        ImGui.PushItemWidth(ImGuiX.Unit(200.0f));
        if (ImGui.BeginCombo("##Brush Mode", BrushModeString(BrushMode)))
        {
            string[] tooltips = {
                "Select tile from Tileset (left), inspect tiles on the subtile viewer (bottom-right).",
                "Draw palette assignments directly onto the tileset; select a palette by right-clicking.",
                "Draw subtiles directly onto the tileset; select by right-clicking on the tileset or subtile viewer.",
                "Draw collision data directly onto the tileset (left-click to set, right-click to clear).",
            };

            foreach (BrushMode mode in new BrushMode[] { BrushMode.Normal, BrushMode.Subtile, BrushMode.Palette, BrushMode.Collision })
            {
                if (ImGui.Selectable(BrushModeString(mode)))
                {
                    BrushMode = mode;
                    OnBrushModeChanged();
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

        // Helper functions for changing subtile properties
        var toggleFlipX = (int t, int tx, int ty) =>
        {
            Project.BeginTransaction("Toggle flip X");
            byte flags = Tileset.GetSubTileFlags(t, tx, ty);
            flags ^= 0x20;
            ForAllSeasons((tileset) => tileset.SetSubTileFlags(t, tx, ty, flags));
            Project.EndTransaction();
        };
        var toggleFlipY = (int t, int tx, int ty) =>
        {
            Project.BeginTransaction("Toggle flip Y");
            byte flags = Tileset.GetSubTileFlags(t, tx, ty);
            flags ^= 0x40;
            ForAllSeasons((tileset) => tileset.SetSubTileFlags(t, tx, ty, flags));
            Project.EndTransaction();
        };
        var togglePriority = (int t, int tx, int ty) =>
        {
            Project.BeginTransaction("Toggle priority");
            byte flags = Tileset.GetSubTileFlags(t, tx, ty);
            flags ^= 0x80;
            ForAllSeasons((tileset) => tileset.SetSubTileFlags(t, tx, ty, flags));
            Project.EndTransaction();
        };

        ImGui.BeginChild("Tileset Viewer Panel", new Vector2(tilesetViewer.WidgetSize.X, HEIGHT));
        ImGui.SeparatorText("Tileset");
        tilesetViewer.Render();

        // Tileset viewer: Watch for keyboard keys 1/2/3 for toggling subtile properties
        if (tilesetViewer.SubTileMode && tilesetViewer.HoveredTile != -1)
        {
            // Change flip X
            if (ImGui.IsKeyPressed(ImGuiKey._1))
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                toggleFlipX(t, tx, ty);
            }
            // Change flip Y
            if (ImGui.IsKeyPressed(ImGuiKey._2))
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                toggleFlipY(t, tx, ty);
            }
            // Change priority
            if (ImGui.IsKeyPressed(ImGuiKey._3))
            {
                var (t, tx, ty) = tilesetViewer.ToSubTileIndex(tilesetViewer.HoveredTile);
                togglePriority(t, tx, ty);
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("SubTile Panel", new Vector2(subTileViewer.WidgetSize.X, HEIGHT));
        ImGui.SeparatorText($"Subtile Viewer");
        subTileViewer.Render();
        ImGui.SeparatorText($"Tile {tileEditor.TileIndex:X2}");
        tileEditor.Render();

        // Tile editor: Watch for keyboard keys 1/2/3 for toggling subtile properties
        if (tileEditor.HoveredTile != -1)
        {
            // Change flip X
            if (ImGui.IsKeyPressed(ImGuiKey._1))
            {
                toggleFlipX(tileEditor.TileIndex, tileEditor.HoveredTile % 2, tileEditor.HoveredTile / 2);
            }
            // Change flip Y
            if (ImGui.IsKeyPressed(ImGuiKey._2))
            {
                toggleFlipY(tileEditor.TileIndex, tileEditor.HoveredTile % 2, tileEditor.HoveredTile / 2);
            }
            // Change priority
            if (ImGui.IsKeyPressed(ImGuiKey._3))
            {
                togglePriority(tileEditor.TileIndex, tileEditor.HoveredTile % 2, tileEditor.HoveredTile / 2);
            }
        }

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

    public void SetTileset(int index, Season season)
    {
        if (Tileset != null && Tileset.Index == index && Tileset.Season == season)
            return;

        SetTileset(Project.GetTileset(index, season, autoCorrect: true));
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
                RealTileset t = Project.GetTileset(Tileset.Index, (Season)s);
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
    void SubTileDrawer(int x, int y, SubTile subTile, bool onTileEditor)
    {
        if (onTileEditor)
        {
            if (!tileEditor.XYValid(x, y))
                return;
        }
        else
        {
            if (!tilesetViewer.XYValid(x, y))
                return;
        }

        Project.BeginTransaction($"Draw subtiles#{Tileset.TransactionIdentifier}", true);

        ForAllSeasons((tileset) =>
        {
            var (t, tx, ty) =
                onTileEditor ? (tileEditor.TileIndex, x, y)
                : tilesetViewer.ToSubTileIndex(x + y * tilesetViewer.Width);

            tileset.SetSubTileIndex(t, tx, ty, (byte)subTile.subtile);
            if (subTile.palette == -1 || !copyPalettes)
                tileset.SetSubTileFlags(t, tx, ty, subTile.GetFlags(tileset.GetSubTilePalette(t, tx, ty)));
            else
                tileset.SetSubTileFlags(t, tx, ty, subTile.GetFlags());
        });

        Project.EndTransaction();
    }

    void OnBrushModeChanged(bool fromConstructor = false)
    {
        tilesetViewer.Selectable = BrushMode == BrushMode.Normal;
        tilesetViewer.SubTileMode = BrushMode != BrushMode.Normal;
        tilesetViewer.BrushInterfacer = BrushMode == BrushMode.Subtile ? subtileBrushInterfacer : null;
        tilesetViewer.TooltipImagePreview = BrushMode != BrushMode.Subtile || subtileBrush.IsSingleTile;

        tileEditor.Selectable = BrushMode == BrushMode.Normal;

        if (BrushMode == BrushMode.Subtile)
        {
            tileEditor.BrushInterfacer = subtileBrushInterfacer;
            subTileViewer.Unselectable = true;
            subTileViewer.Selectable = true;
        }
        else
        {
            tileEditor.BrushInterfacer = null;
            subTileViewer.Selectable = false;
        }

        // The mouse actions we register below here are only enabled situationally. This function
        // unregisters everything and then reregisters only what we need for the current mode.
        tilesetViewer.RemoveMouseAction("Brush LeftClick");
        tilesetViewer.RemoveMouseAction("Brush Ctrl+LeftClick");
        tilesetViewer.RemoveMouseAction("Palette Brush RightClick");
        tilesetViewer.RemoveMouseAction("Subtile Brush RightClick");
        tilesetViewer.RemoveMouseAction("Collision Brush RightClick");
        tilesetViewer.RemoveMouseAction("Collision Brush Ctrl+RightClick");

        subTileViewer.RemoveMouseAction("Subtile Viewer Selection");

        tileEditor.RemoveMouseAction("TileEditor LeftClick");
        tileEditor.RemoveMouseAction("TileEditor Ctrl+LeftClick");
        tileEditor.RemoveMouseAction("TileEditor Collision Brush RightClick");
        tileEditor.RemoveMouseAction("TileEditor Collision Brush Ctrl+RightClick");


        // Helper function for palette brush mode
        var paletteSetter = (TileGridEventArgs args, bool onTileEditor) =>
        {
            Project.BeginTransaction($"Draw palettes#{Tileset.TransactionIdentifier}", true);

            ForAllSeasons((tileset) =>
            {
                args.Foreach((tile) =>
                {
                    var (t, x, y) =
                        onTileEditor ? (tileEditor.TileIndex, tile % 2, tile / 2)
                        : tilesetViewer.ToSubTileIndex(tile);
                    tileset.SetSubTilePalette(t, x, y, selectedPalette);
                });
            });

            Project.EndTransaction();
        };

        // Helper function for collision brush mode
        var collisionSetter = (TileGridEventArgs args, bool enable, bool onTileEditor) =>
        {
            Project.BeginTransaction($"Draw collisions#{Tileset.TransactionIdentifier}", true);

            ForAllSeasons((tileset) =>
            {
                args.Foreach((tile) =>
                {
                    var (t, x, y) =
                        onTileEditor ? (tileEditor.TileIndex, tile % 2, tile / 2)
                        : tilesetViewer.ToSubTileIndex(tile);
                    TrySetSubTileCollision(tileset, t, x, y, enable);
                });
            });

            Project.EndTransaction();
        };

        if (BrushMode != BrushMode.Normal)
        {
            // Function called when drawing on tileset viewer or single tile editor
            var tileClicked = (TileGridEventArgs args, bool onTileEditor) =>
            {
                if (BrushMode == BrushMode.Palette)
                {
                    paletteSetter(args, onTileEditor);
                }
                else if (BrushMode == BrushMode.Subtile)
                {
                    if (args.gridAction == GridAction.SelectRangeCallback)
                    {
                        subtileBrush.Draw((x, y, t) => SubTileDrawer(x, y, t, onTileEditor),
                                          args.topLeft.X,
                                          args.topLeft.Y,
                                          args.SelectedWidth,
                                          args.SelectedHeight
                        );
                    }
                    else
                    {
                        subtileBrush.Draw((x, y, t) => SubTileDrawer(x, y, t, onTileEditor),
                                      args.selectedIndex % args.gridWidth,
                                      args.selectedIndex / args.gridWidth,
                                      subtileBrush.BrushWidth,
                                      subtileBrush.BrushHeight);
                    }
                }
                else if (BrushMode == BrushMode.Collision)
                {
                    collisionSetter(args, true, onTileEditor);
                }
            };

            // Left click: Draw when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    var (t, _, _) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                    tileClicked(args, false);
                    tileEditor.SetTile(Tileset, t);
                },
                name: "Brush LeftClick",
                onRelease: Project.TransactionManager.InsertBarrier
            );

            // Ctrl + left click: Rectangle fill when in a brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    var (t, _, _) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                    tileClicked(args, false);
                    tileEditor.SetTile(Tileset, t);
                },
                brushPreview: true,
                name: "Brush Ctrl+LeftClick",
                onRelease: Project.TransactionManager.InsertBarrier
            );

            // Tile editor left click: Place tiles down
            tileEditor.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    tileClicked(args, true);
                },
                name: "TileEditor LeftClick",
                onRelease: Project.TransactionManager.InsertBarrier
            );

            // Tile editor ctrl + left click: Rectangle fill when in a brush mode
            tileEditor.AddMouseAction(
                MouseButton.LeftClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    tileClicked(args, true);
                },
                name: "TileEditor Ctrl+LeftClick",
                onRelease: Project.TransactionManager.InsertBarrier
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

                        // If we selected just one tile, select it in the tile editor and subtile
                        // view widgets
                        if (subtileBrush.IsSingleTile)
                        {
                            var (t, x, y) = tilesetViewer.ToSubTileIndex(args.topLeft.X + args.topLeft.Y * tilesetViewer.Width);
                            tileEditor.SetTile(Tileset, t);
                            subTileViewer.SelectedIndex = Tileset.GetSubTileIndex(t, x, y) ^ 0x80;
                        }
                        else
                        {
                            subTileViewer.SelectedIndex = -1;
                        }
                    }
                },
                name: "Subtile Brush RightClick"
            );

            // Subtile viewer: Click + drag to select tiles (subtile brush mode only)
            subTileViewer.AddMouseAction(
                MouseButton.Any, MouseModifier.None, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
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
                name: "Subtile Viewer Selection"
            );
        }

        if (BrushMode == BrushMode.Collision)
        {
            // Right click: Unset collision when in collision brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    collisionSetter(args, false, false);
                    var (t, _, _) = tilesetViewer.ToSubTileIndex(args.selectedIndex);
                    tileEditor.SetTile(Tileset, t);
                },
                name: "Collision Brush RightClick",
                onRelease: Project.TransactionManager.InsertBarrier
            );
            // Right click + drag: Unset collision range when in collision brush mode
            tilesetViewer.AddMouseAction(
                MouseButton.RightClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    collisionSetter(args, false, false);
                },
                name: "Collision Brush Ctrl+RightClick",
                onRelease: Project.TransactionManager.InsertBarrier
            );

            // Tile editor right click: Unset collision when in collision brush mode
            tileEditor.AddMouseAction(
                MouseButton.RightClick, MouseModifier.None, MouseAction.ClickDrag, GridAction.Callback,
                (_, args) =>
                {
                    collisionSetter(args, false, true);
                },
                name: "TileEditor Collision Brush RightClick",
                onRelease: Project.TransactionManager.InsertBarrier
            );
            // Tile editor right click + drag: Unset collision range when in collision brush mode
            tileEditor.AddMouseAction(
                MouseButton.RightClick, MouseModifier.Ctrl, MouseAction.ClickDrag, GridAction.SelectRangeCallback,
                (_, args) =>
                {
                    collisionSetter(args, false, true);
                },
                name: "TileEditor Collision Brush Ctrl+RightClick",
                onRelease: Project.TransactionManager.InsertBarrier
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
            TextureBase image = Top.Backend.TextureFromBitmap(bitmap);
            subtileBrushInterfacer.SetPreviewTexture(image, 8);
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
        base.RequestedScale = 2;
        base.Selectable = true;
        base.TooltipImagePreview = true;
        base.TooltipImagePreviewScale = TILE_IMAGE_SCALE;
        base.SelectColor = Color.DarkOrange;

        base.OnHover = (index) =>
        {
            ImGui.BeginTooltip();
            ImGui.PushFont(Top.InfoFont);
            ImGui.Text($"SubTile Index: {index^0x80:X2}");
            ImGui.PopFont();
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
        base.RequestedScale = 4;
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
    TextureBase image;
    EventWrapper<Tileset> tilesetEventWrapper;

    // ================================================================================
    // Properties
    // ================================================================================

    public Project Project { get { return parent.Project; } }
    public RealTileset Tileset { get; private set; }
    public int TileIndex { get; private set; } // Should never be -1

    public override TextureBase Texture { get { return image; } }

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

        ImGuiX.ShiftCursorScreenPos(ImGuiX.Unit(50.0f, 0.0f));
        ImGui.BeginChild("Text fields");

        if (previewIndex == -1)
        {
            // In collision brush mode, instead of rendering subtile properties, just show the
            // collision data for this tile.
            ImGui.PushItemWidth(ImGuiX.Unit(100.0f));
            ImGui.Text("Collision:");
            ImGuiX.InputHex("##Collision", Tileset.GetTileCollision(TileIndex), (c) =>
            {
                Project.BeginTransaction($"Set collision#{Tileset.TransactionIdentifier}-t{TileIndex:X2}", true);
                ForAllSeasons((tileset) => tileset.SetTileCollision(TileIndex, (byte)c));
                Project.EndTransaction();
            });
            ImGui.PopItemWidth();
        }
        else
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

            ImGui.PushItemWidth(ImGuiX.Unit(100.0f));
            ImGui.Text($"({x}, {y})");
            ImGui.Text($"Subtile: ");
            ImGui.SameLine();
            ImGuiX.InputHex("##Subtile",
                            Tileset.GetSubTileIndex(TileIndex, x, y),
                            (v) => Tileset.SetSubTileIndex(TileIndex, x, y, (byte)v),
                            min: 0,
                            max: 255);
            ImGui.Text($"Palette: ");
            ImGui.SameLine();
            ImGuiX.InputHex("##Palette",
                            flags & 7,
                            (v) => Tileset.SetSubTilePalette(TileIndex, x, y, (byte)v),
                            digits: 1,
                            min: 0,
                            max: 7);
            checkboxForBit("Flip X", 0x20);
            checkboxForBit("Flip Y", 0x40);
            checkboxForBit("Priority", 0x80);
            ImGui.PopItemWidth();

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
        // This uses CPU rendering to get the tile, unlike most of our rendering code. Might as well
        // leave like this to ensure that path is still working.
        image = Top.TextureFromBitmapTracked(Tileset.GetTileBitmap(TileIndex));
    }

    /// <summary>
    /// Just like the ForAllSeasons function in TileEditor class
    /// </summary>
    void ForAllSeasons(Action<RealTileset> action)
    {
        parent.ForAllSeasons(action);
    }
}
