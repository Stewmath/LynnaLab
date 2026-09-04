using System.Text;

namespace LynnaLab;

// C# port of imgui_markdown (https://github.com/enkisoftware/imgui_markdown)
//
// Vibe ported on commit: 4acbf805847, targeting ImGui v1.92.2 (HexaGen bindings).
//
// Original License: zlib. Copyright (c) 2019 Juliette Foucaut & Doug Binks
//
// Supported markdown functionality:
//  - Wrapped text
//  - Headers H1, H2, H3
//  - Emphasis
//  - Indented text, multi levels
//  - Unordered lists and sub-lists
//  - Link
//  - Image
//  - Horizontal rule

/// <summary>
/// Renders a subset of markdown using Dear ImGui. Call <see cref="Markdown"/> to render.
/// </summary>
public static class ImGuiMarkdown
{
    // ================================================================================
    // Public types
    // ================================================================================

    [Flags]
    public enum FormatFlags
    {
        None = 0,
        DiscardExtraNewLines = 1 << 0,     // Provided markdown will discard all redundant newlines
        NoNewLineBeforeHeading = 1 << 1,   // Provided markdown will not format a newline before a heading
        SeparatorDoesNotAdvance = 1 << 2,  // Separator will not advance to the next line
        GithubStyle = DiscardExtraNewLines | NoNewLineBeforeHeading | SeparatorDoesNotAdvance,
        CommonMarkAll = DiscardExtraNewLines | NoNewLineBeforeHeading | SeparatorDoesNotAdvance,
    }

    public enum FormatType
    {
        NormalText,
        Heading,
        UnorderedList,
        Link,
        Emphasis,
    }

    /// <summary>
    /// Data passed to link and image callbacks.
    /// </summary>
    public struct LinkCallbackData
    {
        public string Text;    // text between square brackets []
        public string Link;    // text between round brackets ()
        public object UserData;
        public bool IsImage;   // true if '!' is detected in front of the link syntax
    }

    /// <summary>
    /// Data passed to tooltip callbacks.
    /// </summary>
    public struct TooltipCallbackData
    {
        public LinkCallbackData LinkData;
        public string LinkIcon;
    }

    /// <summary>
    /// Returned by an image callback to instruct how to draw an image.
    /// </summary>
    public struct MarkdownImageData
    {
        public bool IsValid;                     // if true, will draw the image
        public bool UseLinkCallback;             // if true, LinkCallback is called when image is clicked
        public ImTextureRef TextureRef;          // see ImGui.ImageWithBg
        public Vector2 Size;                     // see ImGui.ImageWithBg
        public Vector2 Uv0;                      // see ImGui.ImageWithBg
        public Vector2 Uv1;                      // see ImGui.ImageWithBg
        public Vector4 TintCol;                  // see ImGui.ImageWithBg
        public Vector4 BorderCol;                // see ImGui.ImageWithBg
        public Vector4 BgCol;                    // see ImGui.ImageWithBg

        public static MarkdownImageData Default => new MarkdownImageData
        {
            IsValid = false,
            UseLinkCallback = false,
            Size = new Vector2(100.0f, 100.0f),
            Uv0 = new Vector2(0, 0),
            Uv1 = new Vector2(1, 1),
            TintCol = new Vector4(1, 1, 1, 1),
            BorderCol = new Vector4(0, 0, 0, 0),
            BgCol = new Vector4(0, 0, 0, 0),
        };
    }

    public struct MarkdownFormatInfo
    {
        public FormatType Type;
        public int Level;              // Set for headings: 1 for H1, 2 for H2 etc.
        public bool ItemHovered;       // Currently only set for links when hovered, only valid when start == false
        public MarkdownConfig Config;
        public string Text;            // Set for headings
        public int TextLength;
    }

    public struct MarkdownHeadingFormat
    {
        public ImFontPtr Font;         // ImGui font
        public bool Separator;         // if true, an underlined separator is drawn after the header
        public float FontSize;         // Font size if using dynamic fonts (0 = use font's LegacySize)
    }

    /// <summary>
    /// Configuration for markdown rendering.
    /// </summary>
    public class MarkdownConfig
    {
        public const int NumHeadings = 3;

        public Action<LinkCallbackData> LinkCallback;
        public Action<TooltipCallbackData> TooltipCallback;
        public Func<LinkCallbackData, MarkdownImageData> ImageCallback;
        public string LinkIcon = "";
        public MarkdownHeadingFormat[] HeadingFormats = new MarkdownHeadingFormat[NumHeadings]
        {
            new MarkdownHeadingFormat { Separator = true },
            new MarkdownHeadingFormat { Separator = true },
            new MarkdownHeadingFormat { Separator = true },
        };
        public object UserData;
        public Action<MarkdownFormatInfo, bool> FormatCallback = DefaultMarkdownFormatCallback;
        public FormatFlags Flags = FormatFlags.None;
    }

    // ================================================================================
    // Internal state
    // ================================================================================

    // We need to preserve status of link hovering between frames. Store as an index into the
    // markdown buffer (-1 == none), rather than a raw pointer as in the C++ original.
    static int s_linkHoverStart = -1;
    static uint s_linkHoverID = 0;

    enum LinkState
    {
        NoLink,
        HasSquareBracketOpen,
        HasSquareBrackets,
        HasSquareBracketsRoundBracketOpen,
    }

    enum EmphasisState
    {
        None,
        Left,
        Middle,
        Right,
    }

    struct TextBlock
    {
        public int Start;
        public int Stop;
        public readonly int Size => Stop - Start;
    }

    class Line
    {
        public bool IsHeading = false;
        public bool IsEmphasis = false;
        public bool IsUnorderedListStart = false;
        public bool IsLeadingSpace = true;   // spaces at start of line
        public int LeadSpaceCount = 0;
        public int HeadingCount = 0;
        public int EmphasisCount = 0;
        public int LineStart = 0;
        public int LineEnd = 0;
        public int LastRenderPosition = 0;   // lines may get rendered in multiple pieces
    }

    class Link
    {
        public LinkState State = LinkState.NoLink;
        public TextBlock Text;
        public TextBlock Url;
        public bool IsImage = false;
        public int NumBracketsOpen = 0;
    }

    class Emphasis
    {
        public EmphasisState State = EmphasisState.None;
        public TextBlock Text;
        public byte Sym;
    }

    /// <summary>
    /// Handles wrapped text rendering and indentation for the current region. Call
    /// <see cref="ResetIndent"/> before discarding (C++ used a destructor for this).
    /// </summary>
    class TextRegion
    {
        float indentX = 0.0f;

        public unsafe void RenderTextWrapped(byte* text, byte* textEnd, bool indentToHere = false)
        {
            float fontSize = ImGui.GetFontSize();
            float widthLeft = ImGui.GetContentRegionAvail().X;
            byte* endLine = text;
            if (widthLeft > 0.0f)
            {
                endLine = ImGui.GetFont().CalcWordWrapPosition(fontSize, text, textEnd, widthLeft);
            }

            if (endLine > text && endLine < textEnd)
            {
                if (IsCharInsideWord(*endLine))
                {
                    // see if we can do a better cut.
                    float widthNextLine = widthLeft + ImGui.GetCursorScreenPos().X - ImGui.GetWindowPos().X;
                    byte* endNextLine = ImGui.GetFont().CalcWordWrapPosition(fontSize, text, textEnd, widthNextLine);
                    if (endNextLine == textEnd || (endNextLine <= textEnd && !IsCharInsideWord(*endNextLine)))
                    {
                        // can possibly do better if we go to the next line
                        endLine = text;
                    }
                }
            }
            ImGui.TextUnformatted(text, endLine);
            if (indentToHere)
            {
                float indentNeeded = ImGui.GetContentRegionAvail().X - widthLeft;
                if (indentNeeded != 0)
                {
                    ImGui.Indent(indentNeeded);
                    indentX += indentNeeded;
                }
            }
            widthLeft = ImGui.GetContentRegionAvail().X;
            while (endLine < textEnd)
            {
                text = endLine;
                if (*text == (byte)' ') { ++text; }   // skip a space at start of line
                endLine = ImGui.GetFont().CalcWordWrapPosition(fontSize, text, textEnd, widthLeft);
                if (text == endLine)
                {
                    endLine++;
                }
                ImGui.TextUnformatted(text, endLine);
            }
        }

        public unsafe void RenderListTextWrapped(byte* text, byte* textEnd)
        {
            ImGui.Bullet();
            ImGui.SameLine();
            RenderTextWrapped(text, textEnd, true);
        }

        public unsafe bool RenderLinkText(byte* markdown, byte* text, byte* textEnd, Link link,
            MarkdownConfig cfg, ref int linkHoverStart)
        {
            MarkdownFormatInfo formatInfo = new MarkdownFormatInfo();
            formatInfo.Config = cfg;
            formatInfo.Type = FormatType.Link;
            cfg.FormatCallback(formatInfo, true);
            ImGui.PushTextWrapPos(-1.0f);
            ImGui.TextUnformatted(text, textEnd);
            ImGui.PopTextWrapPos();

            bool thisItemHovered = ImGui.IsItemHovered();
            if (thisItemHovered)
            {
                linkHoverStart = link.Text.Start;
            }
            bool hovered = thisItemHovered || (linkHoverStart == link.Text.Start);

            formatInfo.ItemHovered = hovered;
            cfg.FormatCallback(formatInfo, false);

            if (hovered)
            {
                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && cfg.LinkCallback != null)
                {
                    cfg.LinkCallback(MakeLinkData(markdown, link, false));
                }
                if (cfg.TooltipCallback != null)
                {
                    cfg.TooltipCallback(new TooltipCallbackData
                    {
                        LinkData = MakeLinkData(markdown, link, false),
                        LinkIcon = cfg.LinkIcon,
                    });
                }
            }
            return thisItemHovered;
        }

        public unsafe void RenderLinkTextWrapped(byte* markdown, byte* text, byte* textEnd, Link link,
            MarkdownConfig cfg, ref int linkHoverStart, bool indentToHere = false)
        {
            float fontSize = ImGui.GetFontSize();
            float widthLeft = ImGui.GetContentRegionAvail().X;
            byte* endLine = text;
            if (widthLeft > 0.0f)
            {
                endLine = ImGui.GetFont().CalcWordWrapPosition(fontSize, text, textEnd, widthLeft);
            }

            if (endLine > text && endLine < textEnd)
            {
                if (IsCharInsideWord(*endLine))
                {
                    float widthNextLine = widthLeft + ImGui.GetCursorScreenPos().X - ImGui.GetWindowPos().X;
                    byte* endNextLine = ImGui.GetFont().CalcWordWrapPosition(fontSize, text, textEnd, widthNextLine);
                    if (endNextLine == textEnd || (endNextLine <= textEnd && !IsCharInsideWord(*endNextLine)))
                    {
                        endLine = text;
                    }
                }
            }
            bool hovered = RenderLinkText(markdown, text, endLine, link, cfg, ref linkHoverStart);
            if (indentToHere)
            {
                float indentNeeded = ImGui.GetContentRegionAvail().X - widthLeft;
                if (indentNeeded != 0)
                {
                    ImGui.Indent(indentNeeded);
                    indentX += indentNeeded;
                }
            }
            widthLeft = ImGui.GetContentRegionAvail().X;
            while (endLine < textEnd)
            {
                text = endLine;
                if (*text == (byte)' ') { ++text; }   // skip a space at start of line
                endLine = ImGui.GetFont().CalcWordWrapPosition(fontSize, text, textEnd, widthLeft);
                if (text == endLine)
                {
                    endLine++;
                }
                bool thisLineHovered = RenderLinkText(markdown, text, endLine, link, cfg, ref linkHoverStart);
                hovered = hovered || thisLineHovered;
            }
            if (!hovered && linkHoverStart == link.Text.Start)
            {
                linkHoverStart = -1;
            }
        }

        public void ResetIndent()
        {
            if (indentX > 0.0f)
            {
                ImGui.Unindent(indentX);
            }
            indentX = 0.0f;
        }
    }

    // ================================================================================
    // Public interface
    // ================================================================================

    /// <summary>
    /// Render markdown text using the given configuration.
    /// </summary>
    public static unsafe void Markdown(string markdown, MarkdownConfig config)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(markdown);
        int markdownLength = bytes.Length;
        if (markdownLength == 0)
            return;

        fixed (byte* md = bytes)
        {
            RenderMarkdown(md, markdownLength, config);
        }
    }

    // ================================================================================
    // Internal rendering
    // ================================================================================

    static unsafe LinkCallbackData MakeLinkData(byte* markdown, Link link, bool isImage)
    {
        return new LinkCallbackData
        {
            Text = Str(markdown + link.Text.Start, link.Text.Size),
            Link = Str(markdown + link.Url.Start, link.Url.Size),
            IsImage = isImage,
        };
    }

    static unsafe string Str(byte* p, int len)
    {
        if (len <= 0)
            return "";
        return Encoding.UTF8.GetString(p, len);
    }

    static void UnderLine(uint col)
    {
        Vector2 min = ImGui.GetItemRectMin();
        Vector2 max = ImGui.GetItemRectMax();
        min.Y = max.Y;
        ImGui.GetWindowDrawList().AddLine(min, max, col, 1.0f);
    }

    static unsafe void RenderLine(byte* markdown, Line line, TextRegion textRegion, MarkdownConfig cfg)
    {
        // indent
        int indentStart = 0;
        if (line.IsUnorderedListStart)    // ImGui unordered list render always adds one indent
        {
            indentStart = 1;
        }
        for (int j = indentStart; j < line.LeadSpaceCount / 2; ++j)    // add indents
        {
            ImGui.Indent();
        }

        // render
        MarkdownFormatInfo formatInfo = new MarkdownFormatInfo();
        formatInfo.Config = cfg;
        int textStart = line.LastRenderPosition + 1;
        int textSize = line.LineEnd - textStart;
        if (line.IsUnorderedListStart)    // render unordered list
        {
            formatInfo.Type = FormatType.UnorderedList;
            cfg.FormatCallback(formatInfo, true);
            byte* text = markdown + textStart + 1;
            textRegion.RenderListTextWrapped(text, text + textSize - 1);
        }
        else if (line.IsHeading)          // render heading
        {
            formatInfo.Level = line.HeadingCount;
            formatInfo.Type = FormatType.Heading;
            byte* text = markdown + textStart + 1;
            formatInfo.Text = Str(text, textSize - 1);
            formatInfo.TextLength = textSize - 1;
            cfg.FormatCallback(formatInfo, true);
            textRegion.RenderTextWrapped(text, text + textSize - 1);
        }
        else if (line.IsEmphasis)         // render emphasis
        {
            formatInfo.Level = line.EmphasisCount;
            formatInfo.Type = FormatType.Emphasis;
            cfg.FormatCallback(formatInfo, true);
            byte* text = markdown + textStart;
            textRegion.RenderTextWrapped(text, text + textSize);
        }
        else                              // render a normal paragraph chunk
        {
            formatInfo.Type = FormatType.NormalText;
            cfg.FormatCallback(formatInfo, true);
            byte* text = markdown + textStart;
            textRegion.RenderTextWrapped(text, text + textSize);
        }
        cfg.FormatCallback(formatInfo, false);

        // unindent
        for (int j = indentStart; j < line.LeadSpaceCount / 2; ++j)
        {
            ImGui.Unindent();
        }
    }

    static unsafe void RenderMarkdown(byte* markdown, int markdownLength, MarkdownConfig cfg)
    {
        int linkHoverStart = -1;
        uint linkHoverID = ImGui.GetID("MDLHS");
        if (linkHoverID == s_linkHoverID)
        {
            linkHoverStart = s_linkHoverStart;
        }

        Line line = new Line();
        Line prevLine = new Line();
        Link link = new Link();
        Emphasis em = new Emphasis();
        TextRegion textRegion = new TextRegion();
        int concurrentEmptyNewlines = 0;
        bool appliedExtraNewline = false;

        byte c;
        for (int i = 0; i < markdownLength; ++i)
        {
            c = markdown[i];                // get the character at index
            if (c == 0) { break; }          // shouldn't happen but don't go beyond 0.

            // If we're at the beginning of the line, count any spaces
            if (line.IsLeadingSpace)
            {
                if ((cfg.Flags & FormatFlags.DiscardExtraNewLines) != 0) // Discard LF and CRLF newlines by markdown spec
                {
                    if (c == (byte)'\n')
                    {
                        concurrentEmptyNewlines++;
                        line.LineStart += 1;
                        continue;
                    }
                    else if ((c == (byte)'\r') && (markdownLength > i + 1) && (markdown[i + 1] == (byte)'\n'))
                    {
                        concurrentEmptyNewlines++;
                        line.LineStart += 2;
                        i += 1;
                        continue;
                    }
                }

                if (c == (byte)' ')
                {
                    ++line.LeadSpaceCount;
                    continue;
                }
                else
                {
                    line.IsLeadingSpace = false;
                    line.LastRenderPosition = i - 1;
                    if ((c == (byte)'*') && (line.LeadSpaceCount >= 2))
                    {
                        if ((markdownLength > i + 1) && (markdown[i + 1] == (byte)' '))    // space after '*'
                        {
                            line.IsUnorderedListStart = true;
                            ++i;
                            ++line.LastRenderPosition;
                        }
                        // carry on processing as it could be emphasis
                    }
                    else if (c == (byte)'#')
                    {
                        line.HeadingCount++;
                        bool continueChecking = true;
                        int j = i;
                        while (++j < markdownLength && continueChecking)
                        {
                            c = markdown[j];
                            switch (c)
                            {
                                case (byte)'#':
                                    line.HeadingCount++;
                                    break;
                                case (byte)' ':
                                    line.LastRenderPosition = j - 1;
                                    i = j;
                                    line.IsHeading = true;
                                    continueChecking = false;
                                    break;
                                default:
                                    line.IsHeading = false;
                                    continueChecking = false;
                                    break;
                            }
                        }
                        if (line.IsHeading)
                        {
                            // reset emphasis status, we do not support emphasis around headers for now
                            em = new Emphasis();
                            continue;
                        }
                    }
                }
            }

            if ((cfg.Flags & FormatFlags.DiscardExtraNewLines) != 0)
            {
                // In markdown spec, 2 or more consecutive newlines gets converted to a single blank
                // line. The first newline is always digested by this parser so we check for 1 or more here.
                if (!appliedExtraNewline && !prevLine.IsHeading && concurrentEmptyNewlines >= 1)
                {
                    ImGui.NewLine();
                    appliedExtraNewline = true;
                }
            }

            // Test to see if we have a link
            switch (link.State)
            {
                case LinkState.NoLink:
                    if (c == (byte)'[' && !line.IsHeading) // we do not support headings with links for now
                    {
                        link.State = LinkState.HasSquareBracketOpen;
                        link.Text.Start = i + 1;
                        if (i > 0 && markdown[i - 1] == (byte)'!')
                        {
                            link.IsImage = true;
                        }
                    }
                    break;
                case LinkState.HasSquareBracketOpen:
                    if (c == (byte)']')
                    {
                        link.State = LinkState.HasSquareBrackets;
                        link.Text.Stop = i;
                    }
                    break;
                case LinkState.HasSquareBrackets:
                    if (c == (byte)'(')
                    {
                        link.State = LinkState.HasSquareBracketsRoundBracketOpen;
                        link.Url.Start = i + 1;
                        link.NumBracketsOpen = 1;
                    }
                    break;
                case LinkState.HasSquareBracketsRoundBracketOpen:
                    if (c == (byte)'(')
                    {
                        ++link.NumBracketsOpen;
                    }
                    else if (c == (byte)')')
                    {
                        --link.NumBracketsOpen;
                    }
                    if (link.NumBracketsOpen == 0)
                    {
                        // reset emphasis status, we do not support emphasis around links for now
                        em = new Emphasis();
                        // render previous line content
                        line.LineEnd = link.Text.Start - (link.IsImage ? 2 : 1);
                        RenderLine(markdown, line, textRegion, cfg);
                        line.LeadSpaceCount = 0;
                        link.Url.Stop = i;
                        line.IsUnorderedListStart = false;    // the following text shouldn't have bullets
                        ImGui.SameLine(0.0f, 0.0f);
                        if (link.IsImage)   // it's an image, render it.
                        {
                            bool drawnImage = false;
                            bool useLinkCallback = false;
                            if (cfg.ImageCallback != null)
                            {
                                MarkdownImageData imageData = cfg.ImageCallback(MakeLinkData(markdown, link, true));
                                useLinkCallback = imageData.UseLinkCallback;
                                if (imageData.IsValid)
                                {
                                    ImGui.PushStyleColor(ImGuiCol.Border, imageData.BorderCol);
                                    ImGui.ImageWithBg(imageData.TextureRef, imageData.Size, imageData.Uv0,
                                        imageData.Uv1, imageData.BgCol, imageData.TintCol);
                                    ImGui.PopStyleColor();
                                    drawnImage = true;
                                }
                            }
                            if (!drawnImage)
                            {
                                ImGui.Text($"( Image {Str(markdown + link.Url.Start, link.Url.Size)} not loaded )");
                            }
                            if (ImGui.IsItemHovered())
                            {
                                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left) && cfg.LinkCallback != null && useLinkCallback)
                                {
                                    cfg.LinkCallback(MakeLinkData(markdown, link, true));
                                }
                                if (link.Text.Size > 0 && cfg.TooltipCallback != null)
                                {
                                    cfg.TooltipCallback(new TooltipCallbackData
                                    {
                                        LinkData = MakeLinkData(markdown, link, true),
                                        LinkIcon = cfg.LinkIcon,
                                    });
                                }
                            }
                        }
                        else                 // it's a link, render it.
                        {
                            textRegion.RenderLinkTextWrapped(markdown, markdown + link.Text.Start,
                                markdown + link.Text.Start + link.Text.Size, link, cfg, ref linkHoverStart, false);
                        }
                        ImGui.SameLine(0.0f, 0.0f);
                        // reset the link by reinitializing it
                        link = new Link();
                        line.LastRenderPosition = i;
                        break;
                    }
                    break;
            }

            // Test to see if we have emphasis styling
            switch (em.State)
            {
                case EmphasisState.None:
                    if (link.State == LinkState.NoLink && !line.IsHeading)
                    {
                        int next = i + 1;
                        int prev = i - 1;
                        if ((c == (byte)'*' || c == (byte)'_')
                            && (i == line.LineStart
                                || markdown[prev] == (byte)' '
                                || markdown[prev] == (byte)'\t') // emphasis must be preceded by whitespace or line start
                            && markdownLength > next // emphasis must precede non-whitespace
                            && markdown[next] != (byte)' '
                            && markdown[next] != (byte)'\n'
                            && markdown[next] != (byte)'\t')
                        {
                            em.State = EmphasisState.Left;
                            em.Sym = c;
                            em.Text.Start = i;
                            line.EmphasisCount = 1;
                            continue;
                        }
                    }
                    break;
                case EmphasisState.Left:
                    if (em.Sym == c)
                    {
                        ++line.EmphasisCount;
                        continue;
                    }
                    else
                    {
                        em.Text.Start = i;
                        em.State = EmphasisState.Middle;
                    }
                    break;
                case EmphasisState.Middle:
                case EmphasisState.Right:
                    {
                        bool processRight = em.State == EmphasisState.Right;
                        if (em.State == EmphasisState.Middle)
                        {
                            if (em.Sym == c)
                            {
                                em.State = EmphasisState.Right;
                                em.Text.Stop = i;
                                processRight = true;   // pass through to Right handling
                            }
                            // else: no match, leave emphasis in Middle state
                        }
                        if (processRight)
                        {
                            if (em.Sym == c)
                            {
                                if (line.EmphasisCount < 3 && (i - em.Text.Stop + 1 == line.EmphasisCount))
                                {
                                    // render text up to emphasis
                                    int lineEnd = em.Text.Start - line.EmphasisCount;
                                    if (lineEnd > line.LineStart)
                                    {
                                        line.LineEnd = lineEnd;
                                        RenderLine(markdown, line, textRegion, cfg);
                                        ImGui.SameLine(0.0f, 0.0f);
                                        line.IsUnorderedListStart = false;
                                        line.LeadSpaceCount = 0;
                                    }
                                    line.IsEmphasis = true;
                                    line.LastRenderPosition = em.Text.Start - 1;
                                    line.LineStart = em.Text.Start;
                                    line.LineEnd = em.Text.Stop;
                                    RenderLine(markdown, line, textRegion, cfg);
                                    ImGui.SameLine(0.0f, 0.0f);
                                    line.IsEmphasis = false;
                                    line.LastRenderPosition = i;
                                    em = new Emphasis();
                                }
                                continue;
                            }
                            else
                            {
                                em.State = EmphasisState.None;
                                // render text up to here
                                int start = em.Text.Start - line.EmphasisCount;
                                if (start < line.LineStart)
                                {
                                    line.LineEnd = line.LineStart;
                                    line.LineStart = start;
                                    line.LastRenderPosition = start - 1;
                                    RenderLine(markdown, line, textRegion, cfg);
                                    line.LineStart = line.LineEnd;
                                    line.LastRenderPosition = line.LineStart - 1;
                                }
                            }
                        }
                    }
                    break;
            }

            // handle end of line (render)
            if (c == (byte)'\n')
            {
                // first check if the line is a horizontal rule
                line.LineEnd = i;
                if (em.State == EmphasisState.Middle && line.EmphasisCount >= 3 &&
                    (line.LineStart + line.EmphasisCount) == i)
                {
                    ImGui.Separator();
                }
                else
                {
                    // render the line: multiline emphasis requires a complex implementation so not supporting
                    RenderLine(markdown, line, textRegion, cfg);
                }

                // reset the line and emphasis state
                prevLine = line;
                line = new Line();
                em = new Emphasis();

                line.LineStart = i + 1;
                line.LastRenderPosition = i;

                textRegion.ResetIndent();

                // reset the link
                link = new Link();

                concurrentEmptyNewlines = 0;
                appliedExtraNewline = false;
            }
        }

        if (em.State == EmphasisState.Left && line.EmphasisCount >= 3)
        {
            ImGui.Separator();
        }
        else
        {
            // render any remaining text if last char wasn't 0
            if (markdownLength != 0 && line.LineStart < markdownLength && markdown[line.LineStart] != 0)
            {
                // handle both null terminated and non null terminated strings
                line.LineEnd = markdownLength;
                if (0 == markdown[line.LineEnd - 1])
                {
                    --line.LineEnd;
                }
                RenderLine(markdown, line, textRegion, cfg);
            }
        }

        textRegion.ResetIndent();

        if (-1 != linkHoverStart || linkHoverID == s_linkHoverID)
        {
            s_linkHoverStart = linkHoverStart;
            s_linkHoverID = linkHoverID;
        }
    }

    // IsCharInsideWord based on ImGui's CalcWordWrapPositionA
    static bool IsCharInsideWord(byte c)
    {
        return c != (byte)' ' && c != (byte)'.' && c != (byte)',' && c != (byte)';'
            && c != (byte)'!' && c != (byte)'?' && c != (byte)'"';
    }

    // ================================================================================
    // Default callbacks
    // ================================================================================

    public static void DefaultMarkdownTooltipCallback(TooltipCallbackData data)
    {
        if (data.LinkData.IsImage)
        {
            ImGui.SetTooltip(data.LinkData.Link);
        }
        else
        {
            ImGui.SetTooltip($"{data.LinkIcon} Open in browser\n{data.LinkData.Link}");
        }
    }

    public static void DefaultMarkdownFormatCallback(MarkdownFormatInfo info, bool start)
    {
        switch (info.Type)
        {
            case FormatType.NormalText:
                break;
            case FormatType.Emphasis:
            {
                // default styling for emphasis uses the last heading format - for your own styling
                // implement Emphasis in your format callback
                if (info.Level == 1)
                {
                    // normal emphasis
                    if (start)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.TextDisabled));
                    }
                    else
                    {
                        ImGui.PopStyleColor();
                    }
                }
                else
                {
                    // strong emphasis
                    MarkdownHeadingFormat fmt = info.Config.HeadingFormats[MarkdownConfig.NumHeadings - 1];
                    if (start)
                    {
                        if (!fmt.Font.IsNull)
                        {
                            ImGui.PushFont(fmt.Font, 0.0f); // Change font and keep current size
                        }
                    }
                    else
                    {
                        if (!fmt.Font.IsNull)
                        {
                            ImGui.PopFont();
                        }
                    }
                }
                break;
            }
            case FormatType.Heading:
            {
                MarkdownHeadingFormat fmt;
                if (info.Level > MarkdownConfig.NumHeadings)
                {
                    fmt = info.Config.HeadingFormats[MarkdownConfig.NumHeadings - 1];
                }
                else
                {
                    fmt = info.Config.HeadingFormats[info.Level - 1];
                }
                if (start)
                {
                    if ((info.Config.Flags & FormatFlags.NoNewLineBeforeHeading) == 0)
                    {
                        ImGui.NewLine();
                    }
                    if (!fmt.Font.IsNull)
                    {
                        ImGui.PushFont(fmt.Font, fmt.FontSize > 0.0f ? fmt.FontSize : fmt.Font.LegacySize);
                    }
                }
                else
                {
                    if (fmt.Separator)
                    {
                        // In markdown the separator does not advance the cursor
                        Vector2 cursor = ImGui.GetCursorPos();
                        ImGui.Separator();
                        if ((info.Config.Flags & FormatFlags.SeparatorDoesNotAdvance) != 0)
                        {
                            ImGui.SetCursorPos(cursor);
                        }
                    }
                    if (!fmt.Font.IsNull)
                    {
                        ImGui.PopFont();
                    }
                    ImGui.NewLine();
                }
                break;
            }
            case FormatType.UnorderedList:
                break;
            case FormatType.Link:
                if (start)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                }
                else
                {
                    ImGui.PopStyleColor();
                    if (info.ItemHovered)
                    {
                        UnderLine(ImGui.GetColorU32(ImGuiCol.ButtonHovered));
                    }
                    else
                    {
                        UnderLine(ImGui.GetColorU32(ImGuiCol.Button));
                    }
                }
                break;
        }
    }
}
