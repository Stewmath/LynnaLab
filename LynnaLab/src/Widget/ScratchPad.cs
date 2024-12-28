using System.Diagnostics;

namespace LynnaLab;

/// <summary>
/// Provides a space where one can draw tiles and select them for reuse
/// </summary>
public class ScratchPad : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ScratchPad(ProjectWorkspace workspace, string name, TileGrid referenceGrid, Brush<int> brush)
        : base(name)
    {
        this.brush = brush;

        Grid = new ScratchPadGrid(workspace, "Grid", brush, referenceGrid, 32, 32);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Brush<int> brush;

    // ================================================================================
    // Properties
    // ================================================================================

    public TileGrid Grid { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        Grid.Render();
    }

    // ================================================================================
    // Private methods
    // ================================================================================
}

/// <summary>
/// The grid used within a ScratchPad frame
/// </summary>
public class ScratchPadGrid : TileGrid
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public ScratchPadGrid(ProjectWorkspace workspace, string name, Brush<int> brush, TileGrid referenceGrid, int width, int height)
        : base(name)
    {
        base.TileWidth = referenceGrid.TileWidth;
        base.TileHeight = referenceGrid.TileHeight;
        base.Width = width;
        base.Height = height;

        base.InChildWindow = true;
        base.MinScale = 1.0f;
        base.MaxScale = 2.0f;

        base.BrushInterfacer = BrushInterfacer.Create(brush, (index, scale) =>
        {
            if (Workspace.ShowBrushPreview)
                this.referenceGrid.DrawTileImage(index, scale);
        });

        this.Workspace = workspace;
        this.Brush = brush;

        tileGrid = new int[Width, Height];

        // Register mouse buttons for tile selection & placement
        base.RegisterTilePlacementInputs(
            brush,
            (x, y) => GetTile(x, y),
            (x, y, t) => SetTile(x, y, t),
            () => Width,
            () => Height);

        // Redraw this whole image when the reference image is modified. Not the most efficient -
        // will cause a lot of redraws to occur when using the tileset editor.
        referenceImageEventWrapper = new EventWrapper<Image>();
        referenceImageEventWrapper.Bind<ImageModifiedEventArgs>(
            "ModifiedEvent",
            (_, _) => Redraw(),
            weak: false
        );

        image = TopLevel.Backend.CreateImage(Width * TileWidth, Height * TileHeight);
        SetReferenceGrid(referenceGrid);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    int[,] tileGrid;
    Image image;
    Image referenceImage;
    EventWrapper<Image> referenceImageEventWrapper;

    /// <summary>
    /// A TileGrid where each tile index provides the image to use for that value.
    /// It must override the Image property rather than the TileDrawer function for this to work.
    /// </summary>
    TileGrid referenceGrid;

    // ================================================================================
    // Properties
    // ================================================================================

    public override Image Image { get { return image; } }

    ProjectWorkspace Workspace { get; set; }

    Brush<int> Brush { get; set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public void SetTile(int x, int y, int value)
    {
        tileGrid[x, y] = value;
        RedrawTile(XYToTile(x, y));
    }

    public override void Render()
    {
        // Our event handlers don't cover the case where the referenceGrid changes its image (occurs
        // when changing the tileset assigned to the room), so we check that here
        if (referenceImage != referenceGrid.Image)
            SetReferenceGrid(referenceGrid);

        base.Render();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Returns the tile index at the given position in the scratchpad.
    /// </summary>
    int GetTile(int index)
    {
        var (x, y) = TileToXY(index);
        return tileGrid[x, y];
    }

    int GetTile(int x, int y)
    {
        return tileGrid[x, y];
    }

    void Redraw()
    {
        // Ensure there's no funny business with changing the grid size
        Debug.Assert(Image.Width == Width * TileWidth);
        Debug.Assert(Image.Height == Height * TileHeight);

        image.BeginAtomicOperation();
        for (int i=0; i<MaxIndex; i++)
        {
            RedrawTile(i);
        }
        image.EndAtomicOperation();
    }

    void RedrawTile(int index)
    {
        int tile = GetTile(index);
        var (x, y) = referenceGrid.TileToXY(tile);
        var srcPos = new Point(x * TileWidth, y * TileHeight);
        var destPos = new Point(index % Width * TileWidth, index / Width * TileHeight);
        var size = referenceGrid.TileSize;
        referenceGrid.Image.DrawOn(image, srcPos, destPos, size);
    }

    void SetReferenceGrid(TileGrid grid)
    {
        if (referenceGrid != grid || referenceGrid.Image != referenceImage)
        {
            referenceGrid = grid;
            referenceImage = referenceGrid.Image;
            referenceImageEventWrapper.ReplaceEventSource(grid.Image);
            Redraw();
        }
    }
}
