using System.Reflection;
using System.IO;
using Veldrid;
using System.Runtime.CompilerServices;

using ImGuiXCallback = LynnaLab.ImGuiXCallback;
using System.Runtime.InteropServices;

using Point = Util.Point;


namespace VeldridBackend;

/// <summary>
/// A modified version of Veldrid.ImGui's ImGuiRenderer. Manages input for ImGui, and handles all
/// rendering with Veldrid.
///
/// Closely coupled with VeldridBackend. But I try to keep only rendering stuff in here.
/// </summary>
public class ImGuiController : IDisposable
{
    VeldridBackend _backend;
    GraphicsDevice _gd;
    CommandList cl;

    // Uniform buffers for fragment shaders
    DeviceBuffer _fontFragBuffer;
    DeviceBuffer _textureGlobalsBuffer;
    FragGlobalsStruct textureGlobalsStruct; // Used during imgui rendering

    // Other DeviceBuffers
    DeviceBuffer _projMatrixBuffer;
    DeviceBuffer _vertexBuffer;
    DeviceBuffer renderTilesetVertexBuffer;
    DeviceBuffer _indexBuffer;
    DeviceBuffer fullSourceViewportBuffer;

    // More veldrid objects
    Texture _fontTexture;
    TextureView _fontTextureView;
    ResourceLayout _layout;
    ResourceLayout _textureLayout;
    ResourceLayout _renderTilesetLayout;
    Pipeline _pipeline, renderTilesetPipeline;
    ResourceSet _mainResourceSet;
    ResourceSet _fontTextureResourceSet;

    // Whether we've set up the render-to-texture pipeline this frame (gets reset when regular
    // rendering resumes).
    bool renderToTilesetPipelineSetup = false;

    IntPtr _fontAtlasID = (IntPtr)1;

    int _windowWidth;
    int _windowHeight;
    Vector2 _scaleFactor = Vector2.One;

    // Texture trackers
    readonly Dictionary<VeldridTextureBase, ResourceSetInfo> _setsByTexture = new ();
    readonly Dictionary<IntPtr, ResourceSetInfo> _viewsById = new();

    readonly List<IDisposable> _ownedResources = new();
    int _lastAssignedID = 100;


    struct FragGlobalsStruct
    {
        public int InterpolationMode;
        public float alpha;

        public const int sizeInBytes = 8;

        // Must override GetHashCode, Equals functions because default implementations are extremely
        // inefficient on value types for whatever reason. (We need them for dictionaries.)

        public override int GetHashCode() {
            return (int)(0x9e3779b9 * alpha.GetHashCode() + InterpolationMode.GetHashCode());
        }

        public override bool Equals(object obj) {
            return obj is FragGlobalsStruct other
                && other.InterpolationMode == InterpolationMode
                && other.alpha == alpha;
        }
    }

    struct ImageTypeStruct
    {
        public int imageType;
    }

    struct SourceViewportBufferStruct
    {
        public Vector2 topLeft, bottomRight;
    }

    /// <summary>
    /// Constructs a new ImGuiController.
    /// </summary>
    public ImGuiController(VeldridBackend backend, CommandList cl, OutputDescription outputDescription, int width, int height)
    {
        _backend = backend;
        this.cl = cl;
        _gd = backend.GraphicsDevice;

        _windowWidth = width;
        _windowHeight = height;

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard |
            ImGuiConfigFlags.DockingEnable;
        io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;

        CreateDeviceResources(_gd, outputDescription);
        SetPerFrameImGuiData(1f / 60f);
    }

    public VeldridBackend Backend { get { return _backend; } }
    public GraphicsDevice GraphicsDevice { get { return _gd; } }
    public CommandList CommandList { get { return cl; } }

    public void WindowResized(int width, int height)
    {
        _windowWidth = width;
        _windowHeight = height;
    }

    public void DestroyDeviceObjects()
    {
        Dispose();
    }

    public void CreateDeviceResources(GraphicsDevice gd, OutputDescription outputDescription)
    {
        _gd = gd;
        ResourceFactory factory = gd.ResourceFactory;
        _vertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        _vertexBuffer.Name = "ImGui.NET Vertex Buffer";
        renderTilesetVertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        renderTilesetVertexBuffer.Name = "ImGui.NET Vertex Buffer 2";
        _indexBuffer = factory.CreateBuffer(new BufferDescription(2000, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        _indexBuffer.Name = "ImGui.NET Index Buffer";

        _projMatrixBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        _projMatrixBuffer.Name = "ImGui.NET Projection Buffer";

        _textureGlobalsBuffer = _gd.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        _textureGlobalsBuffer.Name = "ImGui.NET Uniform buffer for texture fragment shaders";

        Shader vertexShader = LoadShader(gd.ResourceFactory, "imgui-vertex", ShaderStages.Vertex);
        Shader fragmentShader = LoadShader(gd.ResourceFactory, "imgui-frag", ShaderStages.Fragment);

        VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[]
        {
                new VertexLayoutDescription(
                    new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2),
                    new VertexElementDescription("in_texCoord", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                    new VertexElementDescription("in_color", VertexElementSemantic.Color, VertexElementFormat.Byte4_Norm))
        };

        var shaderSetDescription = new ShaderSetDescription(vertexLayouts, new[] { vertexShader, fragmentShader });

        // Layout for rendering imgui components
        _layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
            new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)
            ));

        // Layout for rendering textures (including font)
        _textureLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("Texture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("FragGlobalsStruct", ResourceKind.UniformBuffer, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("SourceViewportBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
                                                      ));

        GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
            BlendStateDescription.SingleAlphaBlend,
            new DepthStencilStateDescription(false, false, ComparisonKind.Always),
            new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, false, true),
            PrimitiveTopology.TriangleList,
            shaderSetDescription,
            new ResourceLayout[] { _layout, _textureLayout },
            outputDescription,
            ResourceBindingModel.Default);
        _pipeline = factory.CreateGraphicsPipeline(ref pd);

        // Default uniform buffer for font textures, also applies to stuff like rectangle rendering?
        // (Even though it's not in the mainResourceSet?)
        _fontFragBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
        var defaultFragUniformStruct = new FragGlobalsStruct
        {
            InterpolationMode = (int)Interpolation.Nearest,
            alpha = 1.0f,
        };
        _gd.UpdateBuffer(_fontFragBuffer, 0, defaultFragUniformStruct);

        // Uniform buffer for textures. This will be updated frequently, but we set the initial values here.
        textureGlobalsStruct.alpha = 1.0f;
        textureGlobalsStruct.InterpolationMode = (int)Interpolation.Nearest;
        _gd.UpdateBuffer(_textureGlobalsBuffer, 0, textureGlobalsStruct);

        // Default viewport for textures encompasses the whole thing
        fullSourceViewportBuffer = _gd.ResourceFactory.CreateBuffer(
            new BufferDescription((uint)Unsafe.SizeOf<SourceViewportBufferStruct>(), BufferUsage.UniformBuffer));
        SourceViewportBufferStruct viewport = new();
        viewport.topLeft = Vector2.Zero;
        viewport.bottomRight = Vector2.One;
        _gd.UpdateBuffer(fullSourceViewportBuffer, 0, viewport);

        // Resource set for rendering imgui components
        _mainResourceSet = factory.CreateResourceSet(new ResourceSetDescription(
            _layout,
            _projMatrixBuffer,
            gd.PointSampler));

        RecreateFontDeviceTexture(gd);

        _ownedResources.Add(vertexShader);
        _ownedResources.Add(fragmentShader);
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    private ResourceSetInfo RegenerateTextureBinding(VeldridTextureBase texture)
    {
        if (_setsByTexture.TryGetValue(texture, out ResourceSetInfo rsi))
        {
            _ownedResources.Remove(rsi.ResourceSet);
            _gd.DisposeWhenIdle(rsi.ResourceSet);
            _setsByTexture.Remove(texture);
            _viewsById.Remove(rsi.ImGuiBinding);
        }

        ResourceFactory factory = _gd.ResourceFactory;
        Sampler sampler;
        ResourceSet resourceSet;

        // TODO: Maybe just stick to one sampler - updates to textureGlobalsStruct won't affect this later
        if (textureGlobalsStruct.InterpolationMode == (int)Interpolation.Nearest)
        {
            sampler = _gd.PointSampler;
        }
        else if (textureGlobalsStruct.InterpolationMode == (int)Interpolation.Bicubic)
        {
            // This seems to have some effect, but the bulk of the work is in the shader
            sampler = _gd.LinearSampler;
        }
        else
        {
            throw new Exception($"Interpolation method {textureGlobalsStruct.InterpolationMode} unknown");
        }

        Texture baseTexture = null;
        DeviceBuffer sourceViewportBuffer = fullSourceViewportBuffer;

        if (texture is VeldridRgbaTexture t1)
        {
            baseTexture = t1.Texture;
        }
        else if (texture is VeldridTextureWindow t4)
        {
            baseTexture = t4.Texture;

            Vector2 textureSize = new Vector2(baseTexture.Width, baseTexture.Height);

            // References a portion of the texture
            sourceViewportBuffer = _gd.ResourceFactory.CreateBuffer(
                       new BufferDescription((uint)Unsafe.SizeOf<SourceViewportBufferStruct>(), BufferUsage.UniformBuffer));
            SourceViewportBufferStruct viewport = new();
            viewport.topLeft = t4.TopLeft.AsVector2() / textureSize;
            viewport.bottomRight = (t4.TopLeft + t4.Size).AsVector2() / textureSize;
            _gd.UpdateBuffer(sourceViewportBuffer, 0, viewport);

            _ownedResources.Add(sourceViewportBuffer);
        }
        else {
            throw new Exception("Unrecognized texture type: " + texture);
        }

        resourceSet = factory.CreateResourceSet(
            new ResourceSetDescription(_textureLayout,
                                       baseTexture,
                                       sampler,
                                       _textureGlobalsBuffer,
                                       sourceViewportBuffer
            ));

        rsi = new ResourceSetInfo(GetNextImGuiBindingID(), resourceSet, texture);

        _setsByTexture.Add(texture, rsi);
        _viewsById.Add(rsi.ImGuiBinding, rsi);
        _ownedResources.Add(resourceSet);

        return rsi;
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    public IntPtr GetOrCreateImGuiBinding(VeldridTextureBase texture)
    {
        return GetOrCreateResourceSet(texture).ImGuiBinding;
    }

    private ResourceSetInfo GetOrCreateResourceSet(VeldridTextureBase texture)
    {
        if (!_setsByTexture.TryGetValue(texture, out ResourceSetInfo rsi))
        {
            return RegenerateTextureBinding(texture);
        }

        return rsi;
    }

    public void UnbindTexture(VeldridTextureBase texture)
    {
        if (!_setsByTexture.ContainsKey(texture))
            return;
        var rsi = _setsByTexture[texture];
        _ownedResources.Remove(rsi.ResourceSet);
        _gd.DisposeWhenIdle(rsi.ResourceSet);
        _viewsById.Remove(rsi.ImGuiBinding);
        _setsByTexture.Remove(texture);
    }

    private IntPtr GetNextImGuiBindingID()
    {
        int newID = _lastAssignedID++;
        return (IntPtr)newID;
    }

    /// <summary>
    /// Retrieves the shader texture binding for the given helper handle.
    /// </summary>
    private ResourceSetInfo GetTextureResourceSetInfo(IntPtr imGuiBinding)
    {
        if (!_viewsById.TryGetValue(imGuiBinding, out ResourceSetInfo tvi))
        {
            throw new InvalidOperationException("No registered ImGui binding with id " + imGuiBinding.ToString());
        }

        return tvi;
    }

    public void ClearCachedImageResources()
    {
        foreach (IDisposable resource in _ownedResources)
        {
            resource.Dispose();
        }

        _ownedResources.Clear();
        _setsByTexture.Clear();
        _viewsById.Clear();
        _lastAssignedID = 100;
    }

    private Shader LoadShader(ResourceFactory factory, string name, ShaderStages stage)
    {
        byte[] bytes;

        switch (factory.BackendType)
        {
            case GraphicsBackend.Direct3D11:
                {
                    string resourceName = name + ".hlsl.bytes";
                    bytes = GetEmbeddedResourceBytes(resourceName);
                    break;
                }
            case GraphicsBackend.OpenGL:
                {
                    string resourceName = name + ".glsl";
                    bytes = GetEmbeddedResourceBytes(resourceName);
                    break;
                }
            case GraphicsBackend.Vulkan:
                {
                    string resourceName = name + ".spv";
                    bytes = GetEmbeddedResourceBytes(resourceName);
                    break;
                }
            case GraphicsBackend.Metal:
                {
                    string resourceName = name + ".metallib";
                    bytes = GetEmbeddedResourceBytes(resourceName);
                    break;
                }
            default:
                throw new NotImplementedException();
        }

        string entryPoint = "main";

        // Metal backend is untested
        if (_gd.BackendType == GraphicsBackend.Metal)
        {
            if (stage == ShaderStages.Vertex)
                entryPoint = "VS";
            else if (stage == ShaderStages.Fragment)
                entryPoint = "FS";
            else
                throw new NotImplementedException();
        }
        return _gd.ResourceFactory.CreateShader(new ShaderDescription(stage, bytes, entryPoint));
    }

    private byte[] GetEmbeddedResourceBytes(string resourceName)
    {
        Assembly assembly = typeof(ImGuiController).Assembly;
        using (Stream s = assembly.GetManifestResourceStream(resourceName))
        {
            byte[] ret = new byte[s.Length];
            s.Read(ret, 0, (int)s.Length);
            return ret;
        }
    }

    /// <summary>
    /// Recreates the device texture used to render text.
    /// </summary>
    public void RecreateFontDeviceTexture(GraphicsDevice gd)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        // Build
        IntPtr pixels;
        int width, height, bytesPerPixel;
        io.Fonts.GetTexDataAsRGBA32(out pixels, out width, out height, out bytesPerPixel);
        // Store our identifier
        io.Fonts.SetTexID(_fontAtlasID);

        if (_fontTextureResourceSet != null)
            _gd.DisposeWhenIdle(_fontTextureResourceSet);
        if (_fontTexture != null)
            _gd.DisposeWhenIdle(_fontTexture);
        if (_fontTextureView != null)
            _gd.DisposeWhenIdle(_fontTextureView);

        _fontTexture = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
            (uint)width,
            (uint)height,
            1,
            1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Sampled));
        _fontTexture.Name = "ImGui.NET Font Texture";
        gd.UpdateTexture(
            _fontTexture,
            pixels,
            (uint)(bytesPerPixel * width * height),
            0,
            0,
            0,
            (uint)width,
            (uint)height,
            1,
            0,
            0);
        _fontTextureView = gd.ResourceFactory.CreateTextureView(_fontTexture);

        _fontTextureResourceSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
            _textureLayout,
            _fontTextureView,
            _gd.PointSampler,
            _fontFragBuffer,
            fullSourceViewportBuffer));

        io.Fonts.ClearTexData();
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// This method requires a <see cref="GraphicsDevice"/> because it may create new
    /// DeviceBuffers if the size of vertex or index data has increased beyond the capacity of
    /// the existing buffers.
    /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
    /// </summary>
    public void Render(GraphicsDevice gd)
    {
        ImGui.Render();
        RenderImDrawData(ImGui.GetDrawData(), gd);
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds, InputSnapshot snapshot)
    {
        SetPerFrameImGuiData(deltaSeconds);
        UpdateImGuiInput(snapshot);
    }

    /// <summary>
    /// Sets per-frame data based on the associated window.
    /// This is called by Update(float).
    /// </summary>
    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.DisplaySize = new Vector2(
            _windowWidth / _scaleFactor.X,
            _windowHeight / _scaleFactor.Y);
        io.DisplayFramebufferScale = _scaleFactor;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private bool TryMapKey(Key key, out ImGuiKey result)
    {
        ImGuiKey KeyToImGuiKeyShortcut(Key keyToConvert, Key startKey1, ImGuiKey startKey2)
        {
            int changeFromStart1 = (int)keyToConvert - (int)startKey1;
            return startKey2 + changeFromStart1;
        }

        result = key switch
        {
            >= Key.F1 and <= Key.F24 => KeyToImGuiKeyShortcut(key, Key.F1, ImGuiKey.F1),
            >= Key.Keypad0 and <= Key.Keypad9 => KeyToImGuiKeyShortcut(key, Key.Keypad0, ImGuiKey.Keypad0),
            >= Key.A and <= Key.Z => KeyToImGuiKeyShortcut(key, Key.A, ImGuiKey.A),
            >= Key.Number0 and <= Key.Number9 => KeyToImGuiKeyShortcut(key, Key.Number0, ImGuiKey._0),
            Key.ShiftLeft or Key.ShiftRight => ImGuiKey.ModShift,
            Key.ControlLeft or Key.ControlRight => ImGuiKey.ModCtrl,
            Key.AltLeft or Key.AltRight => ImGuiKey.ModAlt,
            Key.WinLeft or Key.WinRight => ImGuiKey.ModSuper,
            Key.Menu => ImGuiKey.Menu,
            Key.Up => ImGuiKey.UpArrow,
            Key.Down => ImGuiKey.DownArrow,
            Key.Left => ImGuiKey.LeftArrow,
            Key.Right => ImGuiKey.RightArrow,
            Key.Enter => ImGuiKey.Enter,
            Key.Escape => ImGuiKey.Escape,
            Key.Space => ImGuiKey.Space,
            Key.Tab => ImGuiKey.Tab,
            Key.BackSpace => ImGuiKey.Backspace,
            Key.Insert => ImGuiKey.Insert,
            Key.Delete => ImGuiKey.Delete,
            Key.PageUp => ImGuiKey.PageUp,
            Key.PageDown => ImGuiKey.PageDown,
            Key.Home => ImGuiKey.Home,
            Key.End => ImGuiKey.End,
            Key.CapsLock => ImGuiKey.CapsLock,
            Key.ScrollLock => ImGuiKey.ScrollLock,
            Key.PrintScreen => ImGuiKey.PrintScreen,
            Key.Pause => ImGuiKey.Pause,
            Key.NumLock => ImGuiKey.NumLock,
            Key.KeypadDivide => ImGuiKey.KeypadDivide,
            Key.KeypadMultiply => ImGuiKey.KeypadMultiply,
            Key.KeypadSubtract => ImGuiKey.KeypadSubtract,
            Key.KeypadAdd => ImGuiKey.KeypadAdd,
            Key.KeypadDecimal => ImGuiKey.KeypadDecimal,
            Key.KeypadEnter => ImGuiKey.KeypadEnter,
            Key.Tilde => ImGuiKey.GraveAccent,
            Key.Minus => ImGuiKey.Minus,
            Key.Plus => ImGuiKey.Equal,
            Key.BracketLeft => ImGuiKey.LeftBracket,
            Key.BracketRight => ImGuiKey.RightBracket,
            Key.Semicolon => ImGuiKey.Semicolon,
            Key.Quote => ImGuiKey.Apostrophe,
            Key.Comma => ImGuiKey.Comma,
            Key.Period => ImGuiKey.Period,
            Key.Slash => ImGuiKey.Slash,
            Key.BackSlash or Key.NonUSBackSlash => ImGuiKey.Backslash,
            _ => ImGuiKey.None
        };

        return result != ImGuiKey.None;
    }

    private void UpdateImGuiInput(InputSnapshot snapshot)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.AddMousePosEvent(snapshot.MousePosition.X, snapshot.MousePosition.Y);
        io.AddMouseButtonEvent(0, snapshot.IsMouseDown(MouseButton.Left));
        io.AddMouseButtonEvent(1, snapshot.IsMouseDown(MouseButton.Right));
        io.AddMouseButtonEvent(2, snapshot.IsMouseDown(MouseButton.Middle));
        io.AddMouseButtonEvent(3, snapshot.IsMouseDown(MouseButton.Button1));
        io.AddMouseButtonEvent(4, snapshot.IsMouseDown(MouseButton.Button2));
        io.AddMouseWheelEvent(0f, snapshot.WheelDelta);
        for (int i = 0; i < snapshot.KeyCharPresses.Count; i++)
        {
            io.AddInputCharacter(snapshot.KeyCharPresses[i]);
        }

        for (int i = 0; i < snapshot.KeyEvents.Count; i++)
        {
            KeyEvent keyEvent = snapshot.KeyEvents[i];
            if (TryMapKey(keyEvent.Key, out ImGuiKey imguikey))
            {
                io.AddKeyEvent(imguikey, keyEvent.Down);
            }
        }
    }

    private void RenderImDrawData(ImDrawDataPtr draw_data, GraphicsDevice gd)
    {
        uint vertexOffsetInVertices = 0;
        uint indexOffsetInElements = 0;

        if (draw_data.CmdListsCount == 0)
        {
            return;
        }

        cl.UpdateBuffer(_textureGlobalsBuffer, 0, textureGlobalsStruct);

        uint totalVBSize = (uint)(draw_data.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>());
        if (totalVBSize > _vertexBuffer.SizeInBytes)
        {
            gd.DisposeWhenIdle(_vertexBuffer);
            _vertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalVBSize * 1.5f), BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        }

        uint totalIBSize = (uint)(draw_data.TotalIdxCount * sizeof(ushort));
        if (totalIBSize > _indexBuffer.SizeInBytes)
        {
            gd.DisposeWhenIdle(_indexBuffer);
            _indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalIBSize * 1.5f), BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        }

        for (int i = 0; i < draw_data.CmdListsCount; i++)
        {
            ImDrawListPtr cmd_list = draw_data.CmdLists[i];

            cl.UpdateBuffer(
                _vertexBuffer,
                vertexOffsetInVertices * (uint)Unsafe.SizeOf<ImDrawVert>(),
                cmd_list.VtxBuffer.Data,
                (uint)(cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()));

            cl.UpdateBuffer(
                _indexBuffer,
                indexOffsetInElements * sizeof(ushort),
                cmd_list.IdxBuffer.Data,
                (uint)(cmd_list.IdxBuffer.Size * sizeof(ushort)));

            vertexOffsetInVertices += (uint)cmd_list.VtxBuffer.Size;
            indexOffsetInElements += (uint)cmd_list.IdxBuffer.Size;
        }

        // Setup orthographic projection matrix into our constant buffer
        ImGuiIOPtr io = ImGui.GetIO();
        Matrix4x4 mvp = Matrix4x4.CreateOrthographicOffCenter(
            0f,
            io.DisplaySize.X,
            io.DisplaySize.Y,
            0.0f,
            -1.0f,
            1.0f);

        cl.UpdateBuffer(_projMatrixBuffer, 0, ref mvp);

        cl.SetVertexBuffer(0, _vertexBuffer);
        cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        cl.SetPipeline(_pipeline);
        cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
        cl.SetGraphicsResourceSet(0, _mainResourceSet);

        draw_data.ScaleClipRects(io.DisplayFramebufferScale);

        // Render command lists
        int vtx_offset = 0;
        int idx_offset = 0;
        for (int n = 0; n < draw_data.CmdListsCount; n++)
        {
            ImDrawListPtr cmd_list = draw_data.CmdLists[n];
            for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmdPtr pcmd = cmd_list.CmdBuffer[cmd_i];
                if (pcmd.UserCallback != IntPtr.Zero)
                {
                    // This doesn't play nice with Vulkan. I think it's because I'm not supposed to
                    // use UpdateBuffer in the middle of a render pass?
                    if (_gd.BackendType != GraphicsBackend.Vulkan)
                    {
                        switch ((ImGuiXCallback)pcmd.UserCallback)
                        {
                            case ImGuiXCallback.SetAlpha:
                                float alpha = Marshal.PtrToStructure<float>(pcmd.UserCallbackData);
                                textureGlobalsStruct.alpha = alpha;
                                cl.UpdateBuffer(_textureGlobalsBuffer, 0, textureGlobalsStruct);
                                break;
                            case ImGuiXCallback.SetInterpolation:
                                int interp = Marshal.PtrToStructure<int>(pcmd.UserCallbackData);
                                textureGlobalsStruct.InterpolationMode = interp;
                                cl.UpdateBuffer(_textureGlobalsBuffer, 0, textureGlobalsStruct);
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    Marshal.FreeHGlobal(pcmd.UserCallbackData);
                }
                else
                {
                    if (pcmd.TextureId != IntPtr.Zero)
                    {
                        if (pcmd.TextureId == _fontAtlasID)
                        {
                            cl.SetGraphicsResourceSet(1, _fontTextureResourceSet);
                        }
                        else
                        {
                            var rsi = GetTextureResourceSetInfo(pcmd.TextureId);
                            cl.SetGraphicsResourceSet(1, rsi.ResourceSet);
                        }
                    }

                    if (pcmd.ClipRect.X >= 0 && pcmd.ClipRect.Y >= 0)
                    {
                        cl.SetScissorRect(
                        0,
                        (uint)pcmd.ClipRect.X,
                        (uint)pcmd.ClipRect.Y,
                        (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X),
                        (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y));
                    }

                    cl.DrawIndexed(pcmd.ElemCount, 1, pcmd.IdxOffset + (uint)idx_offset, (int)pcmd.VtxOffset + vtx_offset, 0);
                }
            }
            vtx_offset += cmd_list.VtxBuffer.Size;
            idx_offset += cmd_list.IdxBuffer.Size;
        }
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _projMatrixBuffer.Dispose();
        _fontTexture.Dispose();
        _fontTextureView.Dispose();
        _layout.Dispose();
        _textureLayout.Dispose();
        _pipeline.Dispose();
        renderTilesetPipeline.Dispose();
        _mainResourceSet.Dispose();

        foreach (IDisposable resource in _ownedResources)
        {
            resource.Dispose();
        }
    }

    void SetupRenderTilesetPipeline(OutputDescription outputDescription)
    {
        Shader vertexShader = LoadShader(_gd.ResourceFactory, "tileset-vertex", ShaderStages.Vertex);
        Shader fragmentShader = LoadShader(_gd.ResourceFactory, "tileset-frag", ShaderStages.Fragment);

        VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[]
        {
                new VertexLayoutDescription(
                    new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2))
        };

        var shaderSet = new ShaderSetDescription(vertexLayouts, new[] { vertexShader, fragmentShader });

        // Layout for rendering a tileset
        _renderTilesetLayout = _gd.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("TilesetGfx", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("TilesetMap", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("TilesetFlags", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("TilesetPalette", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("PointSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

        GraphicsPipelineDescription pipelineDesc = new GraphicsPipelineDescription
        {
            BlendState = BlendStateDescription.SingleAlphaBlend,
            DepthStencilState = DepthStencilStateDescription.Disabled,
            RasterizerState = RasterizerStateDescription.CullNone,
            PrimitiveTopology = PrimitiveTopology.TriangleStrip,
            ResourceLayouts = new[] { _layout, _renderTilesetLayout },
            ShaderSet = shaderSet,
            Outputs = outputDescription,
            ResourceBindingModel = ResourceBindingModel.Default
        };

        renderTilesetPipeline = _gd.ResourceFactory.CreateGraphicsPipeline(ref pipelineDesc);

        _gd.UpdateBuffer(renderTilesetVertexBuffer, 0, new Vector2[] {
            new Vector2(-1.0f, -1.0f),
            new Vector2(1.0f, -1.0f),
            new Vector2(-1.0f, 1.0f),
            new Vector2(1.0f, 1.0f)
        });

        _ownedResources.Add(vertexShader);
        _ownedResources.Add(fragmentShader);
    }

    /// <summary>
    /// This renders a tileset using a custom shader (tileset-frag.glsl) which takes in some raw
    /// tileset data and draws it to a texture.
    /// </summary>
    public unsafe void RenderTileset(VeldridRgbaTexture dest, Tileset tileset)
    {
        // Framebuffer to destination texture
        Framebuffer fb = dest.GetFramebuffer();

        if (renderTilesetPipeline == null)
            SetupRenderTilesetPipeline(fb.OutputDescription);

        cl.SetPipeline(renderTilesetPipeline);
        cl.SetVertexBuffer(0, renderTilesetVertexBuffer);
        cl.SetGraphicsResourceSet(0, _mainResourceSet);
        cl.SetFramebuffer(fb);

        // Texture inputs to shader
        Veldrid.Texture tilesetGfxTexture = null;
        Veldrid.Texture tilesetMapTexture = null;
        Veldrid.Texture tilesetFlagsTexture = null;
        VeldridPalette tilesetPalette = null;

        // Get raw tileset graphics data
        fixed (byte* ptr = tileset.GraphicsState.VramBuffer[1])
        {
            Debug.Assert(tileset.GraphicsState.VramBuffer[1].Length == 0x2000);

            TextureDescription textureDescription = TextureDescription.Texture2D(
                (uint)1,
                (uint)0x800,
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.R16_UInt,
                TextureUsage.Sampled);

            tilesetGfxTexture = _gd.ResourceFactory.CreateTexture(ref textureDescription);
            _gd.UpdateTexture(tilesetGfxTexture, (nint)(ptr + 0x800), 0x800 * 8, 0, 0, 0,
                             1, 0x800, 1, 0, 0);
        }

        var createMapTexture = (byte[] data) =>
        {
            TextureDescription textureDescription = TextureDescription.Texture2D(
                (uint)32,
                (uint)32,
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.R8_UInt,
                TextureUsage.Sampled);

            var texture = _gd.ResourceFactory.CreateTexture(ref textureDescription);
            _gd.UpdateTexture(texture, data, 0, 0, 0,
                             32, 32, 1, 0, 0);
            return texture;
        };

        // Tilemap, flags textures
        byte[] mapData = tileset.GetTileMapBytes();
        byte[] flagData = tileset.GetTileFlagBytes();
        tilesetMapTexture = createMapTexture(mapData);
        tilesetFlagsTexture = createMapTexture(flagData);

        // Palette texture
        var palette = tileset.GraphicsState.GetPalettes(PaletteType.Background).SelectMany(x => x).ToArray();
        tilesetPalette = new VeldridPalette(this, palette, transparentIndex: -1);

        var resourceSet = _gd.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(_renderTilesetLayout,
                                       tilesetGfxTexture,
                                       tilesetMapTexture,
                                       tilesetFlagsTexture,
                                       tilesetPalette.PaletteTexture,
                                       _gd.PointSampler));

        cl.SetGraphicsResourceSet(1, resourceSet);

        cl.Draw(4, 1, 0, 0);

        // Would be more optimal to cache and reuse these when possible
        _gd.DisposeWhenIdle(tilesetGfxTexture);
        _gd.DisposeWhenIdle(tilesetMapTexture);
        _gd.DisposeWhenIdle(tilesetFlagsTexture);
        _gd.DisposeWhenIdle(tilesetPalette);
        _gd.DisposeWhenIdle(resourceSet);
    }

    private struct ResourceSetInfo
    {
        public readonly IntPtr ImGuiBinding;
        public readonly ResourceSet ResourceSet;
        public readonly VeldridTextureBase Texture;

        public ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet, VeldridTextureBase texture)
        {
            ImGuiBinding = imGuiBinding;
            ResourceSet = resourceSet;
            Texture = texture;
        }
    }
}
