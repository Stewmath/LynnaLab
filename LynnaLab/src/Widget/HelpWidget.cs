namespace LynnaLab;

public class HelpWidget
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public HelpWidget(string firstPage = "TOC.md")
    {
        markdownConfig = new ImGuiMarkdown.MarkdownConfig();
        markdownConfig.ImageCallback = MarkdownImageCallback;
        markdownConfig.Flags = ImGuiMarkdown.FormatFlags.NoNewLineBeforeHeading;

        NavigateToNode(firstPage);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    static Dictionary<string, string> markdownFileDict = new();
    static Dictionary<string, ImageData> markdownImageDict = new();

    Stack<NavigationNode> navigationNodesBack = new();
    Stack<NavigationNode> navigationNodesForward = new();
    NavigationNode currentNode;

    ImGuiMarkdown.MarkdownConfig markdownConfig;

    // ================================================================================
    // Properties
    // ================================================================================

    // ================================================================================
    // Public methods
    // ================================================================================

    public void Render(Vector2? childSize = null)
    {
        if (childSize == null)
            childSize = Vector2.Zero;

        ImGuiLL.PushInfoFont();

        ImGui.BeginDisabled(navigationNodesBack.Count == 0);
        if (ImGui.Button("Back"))
                BackNode();
        ImGui.EndDisabled();

        ImGui.BeginDisabled(navigationNodesForward.Count == 0);
        ImGui.SameLine();
        if (ImGui.Button("Forward"))
            ForwardNode();
        ImGui.EndDisabled();

        // Making a different child per-filename gives us automatic saving of the scroll position.
        ImGui.BeginChild(currentNode.filename, (Vector2)childSize);

        markdownConfig.HeadingFormats[0].Font = Top.InfoFontBold;
        markdownConfig.HeadingFormats[0].FontSize = Top.GlobalConfig.InfoFontSize * 1.5f;
        markdownConfig.HeadingFormats[1].Font = Top.InfoFontBold;
        markdownConfig.HeadingFormats[1].FontSize = Top.GlobalConfig.InfoFontSize * 1.3f;
        markdownConfig.HeadingFormats[2].Font = Top.InfoFont;
        markdownConfig.HeadingFormats[2].FontSize = Top.GlobalConfig.InfoFontSize * 1.1f;

        // TODO: Supply project
        markdownConfig.LinkCallback = (d) => MarkdownLinkCallback(null, d);
        markdownConfig.TooltipCallback = (d) => MarkdownTooltipCallback(null, d);

        string data = MarkdownFileContents(currentNode.filename);
        ImGuiMarkdown.Markdown(data, markdownConfig);

        ImGui.EndChild();
        ImGui.PopFont();
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    [MemberNotNull(nameof(currentNode))]
    void NavigateToNode(string filename)
    {
        Console.WriteLine("Navigate: " + filename);
        if (currentNode != null)
            navigationNodesBack.Push(currentNode);
        currentNode = new() { filename=filename };
        navigationNodesForward.Clear();
    }

    bool BackNode()
    {
        if (navigationNodesBack.Count == 0)
            return false;
        navigationNodesForward.Push(currentNode);
        currentNode = navigationNodesBack.Pop();
        return true;
    }

    bool ForwardNode()
    {
        if (navigationNodesForward.Count == 0)
            return false;
        navigationNodesBack.Push(currentNode);
        currentNode = navigationNodesForward.Pop();
        return true;
    }

    void MarkdownLinkCallback(Project? project, ImGuiMarkdown.LinkCallbackData data)
    {
        string link = data.Link;
        if (link.StartsWith("disasm:"))
        {
            if (project?.BaseDirectory == null) // Clients don't have a file to open
            {
                Modal.DisplayErrorMessage("Can't follow link (no local project is open).");
                return;
            }
            link = project.BaseDirectory + "/"
                   + link.Substring(7).Replace("{game}", project.GameString);
        }
        else if (link.StartsWith("http"))
        {
        }
        else
        {
            NavigateToNode(link);
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = link,
            UseShellExecute = true,
        });
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// Retrieve cached text from a markdown file for rendering
    /// </summary>
    static string MarkdownFileContents(string filename)
    {
        if (markdownFileDict.TryGetValue(filename, out string? content))
        {
            return content;
        }

        content = Helper.ReadResourceFile($"Docs/{filename}");
        markdownFileDict[filename] = content;
        return content;
    }

    /// <summary>
    /// Store a bit of extra data for cached markdown images
    /// </summary>
    class ImageData
    {
        public ImGuiMarkdown.MarkdownImageData data;
        public Vector2 baseSize;
    }

    /// <summary>
    /// Retrieve a saved image & metadata for markdown rendering
    /// </summary>
    static ImGuiMarkdown.MarkdownImageData MarkdownImageCallback(ImGuiMarkdown.LinkCallbackData linkData)
    {
        VeldridBackend.VeldridRgbaTexture texture;

        if (markdownImageDict.TryGetValue(linkData.Link, out ImageData? imgData))
        {
            goto end;
        }

        imgData = new();
        imgData.data = ImGuiMarkdown.MarkdownImageData.Default;
        imgData.data.IsValid = true;
        using (var bitmap = new Bitmap(Helper.ReadResourceFileBytes($"Docs/{linkData.Link}")))
        {
            texture = Top.Backend.TextureFromBitmap(bitmap);
            imgData.data.TextureRef = texture.GetBinding();
            imgData.baseSize = new Vector2(bitmap.Width, bitmap.Height);
        }

        markdownImageDict[linkData.Link] = imgData;

        end:

        // Divide size by 1.6f because that's the DPI scale of my laptop where I'm taking the
        // screenshots
        imgData.data.Size = ImGuiX.Unit(imgData.baseSize) / 1.6f;

        return imgData.data;
    }

    static void MarkdownTooltipCallback(Project? project, ImGuiMarkdown.TooltipCallbackData data)
    {
        if (data.LinkData.Link != null)
        {
            string text = data.LinkData.Link;
            if (project != null)
                text = data.LinkData.Link.Replace("{game}", project.GameString);
            ImGui.SetTooltip(text);
        }
    }
}

record NavigationNode
{
    public required string filename;
}
