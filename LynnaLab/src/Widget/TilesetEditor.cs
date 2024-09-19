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

        tilesetViewer.SelectedEvent += (selectedIndex) =>
        {
            tileEditor.SetTile(Tileset, selectedIndex);
        };

        SetTileset(0, 1);
    }

    // ================================================================================
    // Variables
    // ================================================================================
    TilesetViewer tilesetViewer;
    SubTileViewer subTileViewer;
    TileEditor tileEditor;

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
        ImGuiLL.RenderPaletteHeader(Tileset.PaletteHeaderGroup);
        ImGui.EndChild();

        ImGui.SeparatorText("Tileset Properties");
        ImGuiLL.RenderTilesetFields(Tileset);
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
