namespace LynnaLab;

public class TilesetCloner : Frame
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public TilesetCloner(ProjectWorkspace workspace, string name)
        : base(name)
    {
        this.Workspace = workspace;
        sourceViewer = new TilesetViewer(workspace);
        previewViewer = new TilesetViewer(workspace);

        SetSourceTileset(Project.GetTileset(0, 0));
        SetDestTileset(Project.GetTileset(0, 3));

        sourceViewer.Selectable = false;
        previewViewer.Selectable = false;
        sourceViewer.CenterX = true;
        previewViewer.CenterX = true;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    TilesetViewer sourceViewer;
    TilesetViewer previewViewer;

    bool copyGraphics, copyTileMap, copyCollisions, copyProperties;

    RealTileset sourceTileset, destTileset;
    FakeTileset previewTileset;

    bool changedDestTileset;

    // ================================================================================
    // Properties
    // ================================================================================

    public ProjectWorkspace Workspace { get; private set; }
    public Project Project { get { return Workspace.Project; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void Render()
    {
        Vector2 panelSize = sourceViewer.WidgetSize;
        panelSize.Y += 80.0f;

        changedDestTileset = false;

        ImGui.BeginChild("Source Panel", panelSize);
        ImGui.SeparatorText("From");
        ImGuiLL.TilesetChooser(Project, "Source Tileset", sourceTileset.Index, sourceTileset.Season,
                               (index, season) =>
        {
            SetSourceTileset(Project.GetTileset(index, season));
        });
        sourceViewer.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Dest Panel", panelSize);
        ImGui.SeparatorText("To (Preview)");
        ImGuiLL.TilesetChooser(Project, "Preview Tileset", destTileset.Index, destTileset.Season,
                               (index, season) =>
        {
            SetDestTileset(Project.GetTileset(index, season));
            changedDestTileset = true;
        });

        previewViewer.Render();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("Copy Panel", panelSize);
        ImGui.SeparatorText("Data to copy");

        // Lambda for defining a checkbox to copy data to the preview tileset
        var copyButton = (string name, ref bool boolean, Action<Tileset> loader, string tooltip) =>
        {
            if (ImGui.Checkbox(name, ref boolean) || changedDestTileset)
            {
                if (boolean)
                    loader(sourceTileset);
                else
                    loader(destTileset);
            }
            if (ImGui.IsItemHovered())
                ImGuiX.Tooltip(tooltip);
        };

        copyButton("Copy Graphics", ref copyGraphics, previewTileset.LoadGraphics,
                   "Copy graphics (PNG file contents).\n\nHack-base expands tileset graphics such that no two tilesets reuse any graphical data.");
        copyButton("Copy Tilemap/flags",
                   ref copyTileMap,
                   (t) =>
                   {
                       previewTileset.LoadSubTileIndices(t);
                       previewTileset.LoadSubTileFlags(t);
                   },
                   "The tile map determines which subtiles are mapped to which tiles.\nThe tile flags determine palette, flip X/Y, and priority.\n\nIn vanilla these values are identical between seasonal variants, but in hack-base they can be modified independantly per-season.");
        copyButton("Copy Collisions", ref copyCollisions, previewTileset.LoadCollisions,
                   "Per-tile collision data (only affects solidity, not other properties like warps, etc).");
        copyButton("Copy properties", ref copyProperties, previewTileset.LoadProperties,
                   "Copy all properties (bottom panel) from the source tileset.");

        ImGui.EndChild();

        ImGui.SeparatorText("Tileset Properties (for preview)");
        ImGuiLL.RenderTilesetFields(previewTileset, Workspace.ShowDocumentation);

        ImGui.Separator();
        if (ImGui.Button("Apply Tileset Changes"))
        {
            destTileset.LoadFrom(previewTileset);
        }
        if (ImGui.IsItemHovered())
        {
            ImGuiX.Tooltip("Any changes to the tileset preview are not applied until this button is clicked.");
        }
    }

    public void SetSourceTileset(RealTileset tileset)
    {
        sourceTileset = tileset;
        sourceViewer.SetTileset(sourceTileset);
    }

    public void SetDestTileset(RealTileset tileset)
    {
        destTileset = tileset;
        ReloadPreview();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void ReloadPreview()
    {
        previewTileset?.Dispose();
        previewTileset = new FakeTileset(destTileset);
        previewViewer.SetTileset(previewTileset);
    }
}
