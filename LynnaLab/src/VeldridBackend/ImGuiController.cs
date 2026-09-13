#nullable disable
using System.Reflection;
using System.IO;
using Veldrid;
using System.Runtime.CompilerServices;

using ImGuiXCallback = LynnaLab.ImGuiXCallback;
using System.Runtime.InteropServices;

using Point = Util.Point;
using SDL_Keycode = SDL.SDL_Keycode;
using SDLUtil;


namespace VeldridBackend;

/// <summary>
/// A modified version of Veldrid.ImGui's ImGuiRenderer. Manages input for ImGui, and handles all
/// rendering with Veldrid.
///
/// Closely coupled with VeldridBackend. But I try to keep only rendering stuff in here.
/// </summary>
public class ImGuiController : IDisposable
{
    readonly ImTextureID IMTEXTURE_INVALID = (ImTextureID)(nint)0;

    VeldridBackend backend;
    GraphicsDevice gd;
    CommandList cl;

    // Uniform buffers for fragment shaders
    DeviceBuffer fontFragBuffer;
    DeviceBuffer textureGlobalsBuffer;
    FragGlobalsStruct textureGlobalsStruct; // Used during imgui rendering

    // Other DeviceBuffers
    DeviceBuffer projMatrixBuffer;
    DeviceBuffer vertexBuffer;
    DeviceBuffer renderTilesetVertexBuffer;
    DeviceBuffer indexBuffer;
    DeviceBuffer fullSourceViewportBuffer;

    // More veldrid objects
    ResourceLayout layout;
    ResourceLayout textureLayout;
    ResourceLayout renderTilesetLayout;
    Pipeline pipeline, renderTilesetPipeline;
    ResourceSet mainResourceSet;

    // A GCHandle to this controller, handed to ImGuiX so that render callbacks can dispatch back
    // into instance methods (through the reference stored in their data argument).
    GCHandle selfHandle;

    // Texture trackers
    readonly Dictionary<VeldridTextureBase, ResourceSetInfo> setsByTexture = new ();
    readonly Dictionary<IntPtr, ResourceSetInfo> viewsById = new();

    readonly List<IDisposable> ownedResources = new();
    int lastAssignedID = 100;

    bool textInputActive = false;


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

    struct SourceViewportBufferStruct
    {
        public Vector2 topLeft, bottomRight;
    }

    /// <summary>
    /// Constructs a new ImGuiController.
    /// </summary>
    public ImGuiController(VeldridBackend backend, CommandList cl, OutputDescription outputDescription)
    {
        this.backend = backend;
        this.cl = cl;
        this.gd = backend.GraphicsDevice;

        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset
                           | ImGuiBackendFlags.RendererHasTextures;
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard |
            ImGuiConfigFlags.DockingEnable;
        io.Fonts.Flags |= ImFontAtlasFlags.NoBakedLines;

        CreateDeviceResources(outputDescription);
        SetPerFrameImGuiData(1f / 60f);

        // Register ourselves as the target that render callbacks dispatch into.
        selfHandle = GCHandle.Alloc(this);
        LynnaLab.ImGuiX.SetRenderCallbackTarget(GCHandle.ToIntPtr(selfHandle));
    }

    public VeldridBackend Backend { get { return backend; } }
    public GraphicsDevice GraphicsDevice { get { return gd; } }
    public CommandList CommandList { get { return cl; } }

    public void DestroyDeviceObjects()
    {
        Dispose();
    }

    public void CreateDeviceResources(OutputDescription outputDescription)
    {
        ResourceFactory factory = gd.ResourceFactory;
        vertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        vertexBuffer.Name = "ImGui.NET Vertex Buffer";
        renderTilesetVertexBuffer = factory.CreateBuffer(new BufferDescription(10000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        renderTilesetVertexBuffer.Name = "ImGui.NET Vertex Buffer 2";
        indexBuffer = factory.CreateBuffer(new BufferDescription(2000, BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        indexBuffer.Name = "ImGui.NET Index Buffer";

        projMatrixBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        projMatrixBuffer.Name = "ImGui.NET Projection Buffer";

        textureGlobalsBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        textureGlobalsBuffer.Name = "ImGui.NET Uniform buffer for texture fragment shaders";

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
        layout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("ProjectionMatrixBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
            new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)
            ));

        // Layout for rendering textures (including font)
        textureLayout = factory.CreateResourceLayout(new ResourceLayoutDescription(
            new ResourceLayoutElementDescription("PointTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("PointSampler", ResourceKind.Sampler, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("BilinearTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("BilinearSampler", ResourceKind.Sampler, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("FragGlobalsStruct", ResourceKind.UniformBuffer, ShaderStages.Fragment),
            new ResourceLayoutElementDescription("SourceViewportBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex)
                                                      ));

        GraphicsPipelineDescription pd = new GraphicsPipelineDescription(
            BlendStateDescription.SingleAlphaBlend,
            new DepthStencilStateDescription(false, false, ComparisonKind.Always),
            new RasterizerStateDescription(FaceCullMode.None, PolygonFillMode.Solid, FrontFace.Clockwise, false, true),
            PrimitiveTopology.TriangleList,
            shaderSetDescription,
            new ResourceLayout[] { layout, textureLayout },
            outputDescription,
            ResourceBindingModel.Default);
        pipeline = factory.CreateGraphicsPipeline(ref pd);

        // Default uniform buffer for font textures, also applies to stuff like rectangle rendering?
        // (Even though it's not in the mainResourceSet?)
        fontFragBuffer = factory.CreateBuffer(new BufferDescription(16, BufferUsage.UniformBuffer));
        var defaultFragUniformStruct = new FragGlobalsStruct
        {
            InterpolationMode = (int)Interpolation.Nearest,
            alpha = 1.0f,
        };
        gd.UpdateBuffer(fontFragBuffer, 0, defaultFragUniformStruct);

        // Uniform buffer for textures. This will be updated frequently, but we set the initial values here.
        textureGlobalsStruct.alpha = 1.0f;
        textureGlobalsStruct.InterpolationMode = (int)Interpolation.Nearest;
        gd.UpdateBuffer(textureGlobalsBuffer, 0, textureGlobalsStruct);

        // Default viewport for textures encompasses the whole thing
        fullSourceViewportBuffer = gd.ResourceFactory.CreateBuffer(
            new BufferDescription((uint)Unsafe.SizeOf<SourceViewportBufferStruct>(), BufferUsage.UniformBuffer));
        SourceViewportBufferStruct viewport = new();
        viewport.topLeft = Vector2.Zero;
        viewport.bottomRight = Vector2.One;
        gd.UpdateBuffer(fullSourceViewportBuffer, 0, viewport);

        // Resource set for rendering imgui components
        mainResourceSet = factory.CreateResourceSet(new ResourceSetDescription(
            layout,
            projMatrixBuffer,
            gd.PointSampler));

        ownedResources.Add(vertexShader);
        ownedResources.Add(fragmentShader);
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    private ResourceSetInfo RegenerateTextureBinding(VeldridTextureBase texture)
    {
        if (setsByTexture.TryGetValue(texture, out ResourceSetInfo rsi))
        {
            ownedResources.Remove(rsi.ResourceSet);
            gd.DisposeWhenIdle(rsi.ResourceSet);
            setsByTexture.Remove(texture);
            viewsById.Remove(rsi.ImGuiBinding);
        }

        ResourceFactory factory = gd.ResourceFactory;
        ResourceSet resourceSet;

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
            sourceViewportBuffer = gd.ResourceFactory.CreateBuffer(
                       new BufferDescription((uint)Unsafe.SizeOf<SourceViewportBufferStruct>(), BufferUsage.UniformBuffer));
            SourceViewportBufferStruct viewport = new();
            viewport.topLeft = t4.TopLeft.AsVector2() / textureSize;
            viewport.bottomRight = (t4.TopLeft + t4.Size).AsVector2() / textureSize;
            gd.UpdateBuffer(sourceViewportBuffer, 0, viewport);

            ownedResources.Add(sourceViewportBuffer);
        }
        else {
            throw new Exception("Unrecognized texture type: " + texture);
        }

        resourceSet = factory.CreateResourceSet(
            new ResourceSetDescription(textureLayout,
                                       baseTexture,
                                       gd.PointSampler,
                                       baseTexture,
                                       gd.LinearSampler,
                                       textureGlobalsBuffer,
                                       sourceViewportBuffer
            ));

        rsi = new ResourceSetInfo(GetNextImGuiBindingID(), resourceSet, texture);

        setsByTexture.Add(texture, rsi);
        viewsById.Add(rsi.ImGuiBinding, rsi);
        ownedResources.Add(resourceSet);

        return rsi;
    }

    /// <summary>
    /// Gets or creates a handle for a texture to be drawn with ImGui.
    /// Pass the returned handle to Image() or ImageButton().
    /// </summary>
    public ImTextureRef GetOrCreateImGuiBinding(VeldridTextureBase texture)
    {
        return GetOrCreateResourceSet(texture).TextureRef;
    }

    private ResourceSetInfo GetOrCreateResourceSet(VeldridTextureBase texture)
    {
        if (!setsByTexture.TryGetValue(texture, out ResourceSetInfo rsi))
        {
            return RegenerateTextureBinding(texture);
        }

        return rsi;
    }

    public void UnbindTexture(VeldridTextureBase texture)
    {
        if (!setsByTexture.ContainsKey(texture))
            return;
        var rsi = setsByTexture[texture];
        ownedResources.Remove(rsi.ResourceSet);
        gd.DisposeWhenIdle(rsi.ResourceSet);
        viewsById.Remove(rsi.ImGuiBinding);
        setsByTexture.Remove(texture);
    }

    private IntPtr GetNextImGuiBindingID()
    {
        int newID = lastAssignedID++;
        return (IntPtr)newID;
    }

    /// <summary>
    /// Retrieves the shader texture binding for the given helper handle.
    /// </summary>
    private ResourceSetInfo GetTextureResourceSetInfo(IntPtr imGuiBinding)
    {
        if (!viewsById.TryGetValue(imGuiBinding, out ResourceSetInfo tvi))
        {
            throw new InvalidOperationException("No registered ImGui binding with id " + imGuiBinding.ToString());
        }

        return tvi;
    }

    public void ClearCachedImageResources()
    {
        foreach (IDisposable resource in ownedResources)
        {
            resource.Dispose();
        }

        ownedResources.Clear();
        setsByTexture.Clear();
        viewsById.Clear();
        lastAssignedID = 100;
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
        if (gd.BackendType == GraphicsBackend.Metal)
        {
            if (stage == ShaderStages.Vertex)
                entryPoint = "VS";
            else if (stage == ShaderStages.Fragment)
                entryPoint = "FS";
            else
                throw new NotImplementedException();
        }
        return gd.ResourceFactory.CreateShader(new ShaderDescription(stage, bytes, entryPoint));
    }

    private byte[] GetEmbeddedResourceBytes(string resourceName)
    {
        Assembly assembly = typeof(ImGuiController).Assembly;
        using (Stream s = assembly.GetManifestResourceStream(resourceName))
        {
            byte[] ret = new byte[s.Length];
            s.ReadExactly(ret, 0, (int)s.Length);
            return ret;
        }
    }

    /// <summary>
    /// Renders the ImGui draw list data.
    /// This method requires a <see cref="GraphicsDevice"/> because it may create new
    /// DeviceBuffers if the size of vertex or index data has increased beyond the capacity of
    /// the existing buffers.
    /// A <see cref="CommandList"/> is needed to submit drawing and resource update commands.
    /// </summary>
    public void Render()
    {
        ImGui.Render();
        RenderImDrawData(ImGui.GetDrawData());
    }

    /// <summary>
    /// Updates ImGui input and IO configuration state.
    /// </summary>
    public void Update(float deltaSeconds, SDLUtil.InputSnapshot snapshot)
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
            backend.FramebufferSize.X,
            backend.FramebufferSize.Y);
        io.DisplayFramebufferScale = Vector2.One;
        io.DeltaTime = deltaSeconds; // DeltaTime is in seconds.
    }

    private bool TryMapKey(SDL_Keycode key, out ImGuiKey result)
    {
        ImGuiKey KeyToImGuiKeyShortcut(SDL_Keycode keyToConvert, SDL_Keycode startKey1, ImGuiKey startKey2)
        {
            int changeFromStart1 = (int)keyToConvert - (int)startKey1;
            return startKey2 + changeFromStart1;
        }

        result = key switch
        {
            >= SDL_Keycode.SDLK_F1 and <= SDL_Keycode.SDLK_F12 => KeyToImGuiKeyShortcut(key, SDL_Keycode.SDLK_F1, ImGuiKey.F1),
            SDL_Keycode.SDLK_KP_0 => ImGuiKey.Keypad0,
            >= SDL_Keycode.SDLK_KP_1 and <= SDL_Keycode.SDLK_KP_9 => KeyToImGuiKeyShortcut(key, SDL_Keycode.SDLK_KP_1, ImGuiKey.Keypad1),
            >= SDL_Keycode.SDLK_A and <= SDL_Keycode.SDLK_Z => KeyToImGuiKeyShortcut(key, SDL_Keycode.SDLK_A, ImGuiKey.A),
            >= SDL_Keycode.SDLK_0 and <= SDL_Keycode.SDLK_9 => KeyToImGuiKeyShortcut(key, SDL_Keycode.SDLK_0, ImGuiKey.Key0),
            SDL_Keycode.SDLK_LSHIFT => ImGuiKey.LeftShift,
            SDL_Keycode.SDLK_RSHIFT => ImGuiKey.RightShift,
            SDL_Keycode.SDLK_LCTRL => ImGuiKey.LeftCtrl,
            SDL_Keycode.SDLK_RCTRL => ImGuiKey.RightCtrl,
            SDL_Keycode.SDLK_LALT => ImGuiKey.LeftAlt,
            SDL_Keycode.SDLK_RALT => ImGuiKey.RightAlt,
            SDL_Keycode.SDLK_LGUI => ImGuiKey.LeftSuper,
            SDL_Keycode.SDLK_RGUI => ImGuiKey.RightSuper,
            SDL_Keycode.SDLK_MENU => ImGuiKey.Menu,
            SDL_Keycode.SDLK_UP => ImGuiKey.UpArrow,
            SDL_Keycode.SDLK_DOWN => ImGuiKey.DownArrow,
            SDL_Keycode.SDLK_LEFT => ImGuiKey.LeftArrow,
            SDL_Keycode.SDLK_RIGHT => ImGuiKey.RightArrow,
            SDL_Keycode.SDLK_RETURN => ImGuiKey.Enter,
            SDL_Keycode.SDLK_ESCAPE => ImGuiKey.Escape,
            SDL_Keycode.SDLK_SPACE => ImGuiKey.Space,
            SDL_Keycode.SDLK_TAB => ImGuiKey.Tab,
            SDL_Keycode.SDLK_BACKSPACE => ImGuiKey.Backspace,
            SDL_Keycode.SDLK_INSERT => ImGuiKey.Insert,
            SDL_Keycode.SDLK_DELETE => ImGuiKey.Delete,
            SDL_Keycode.SDLK_PAGEUP => ImGuiKey.PageUp,
            SDL_Keycode.SDLK_PAGEDOWN => ImGuiKey.PageDown,
            SDL_Keycode.SDLK_HOME => ImGuiKey.Home,
            SDL_Keycode.SDLK_END => ImGuiKey.End,
            SDL_Keycode.SDLK_CAPSLOCK => ImGuiKey.CapsLock,
            SDL_Keycode.SDLK_SCROLLLOCK => ImGuiKey.ScrollLock,
            SDL_Keycode.SDLK_PRINTSCREEN => ImGuiKey.PrintScreen,
            SDL_Keycode.SDLK_PAUSE => ImGuiKey.Pause,
            SDL_Keycode.SDLK_NUMLOCKCLEAR => ImGuiKey.NumLock,
            SDL_Keycode.SDLK_KP_MEMDIVIDE => ImGuiKey.KeypadDivide,
            SDL_Keycode.SDLK_KP_MEMMULTIPLY => ImGuiKey.KeypadMultiply,
            SDL_Keycode.SDLK_KP_MEMSUBTRACT => ImGuiKey.KeypadSubtract,
            SDL_Keycode.SDLK_KP_MEMADD => ImGuiKey.KeypadAdd,
            SDL_Keycode.SDLK_KP_DECIMAL => ImGuiKey.KeypadDecimal,
            SDL_Keycode.SDLK_KP_ENTER => ImGuiKey.KeypadEnter,
            SDL_Keycode.SDLK_TILDE => ImGuiKey.GraveAccent,
            SDL_Keycode.SDLK_MINUS => ImGuiKey.Minus,
            SDL_Keycode.SDLK_PLUS => ImGuiKey.Equal,
            SDL_Keycode.SDLK_LEFTBRACKET => ImGuiKey.LeftBracket,
            SDL_Keycode.SDLK_RIGHTBRACKET => ImGuiKey.RightBracket,
            SDL_Keycode.SDLK_SEMICOLON => ImGuiKey.Semicolon,
            SDL_Keycode.SDLK_APOSTROPHE => ImGuiKey.Apostrophe,
            SDL_Keycode.SDLK_COMMA => ImGuiKey.Comma,
            SDL_Keycode.SDLK_PERIOD => ImGuiKey.Period,
            SDL_Keycode.SDLK_SLASH => ImGuiKey.Slash,
            SDL_Keycode.SDLK_BACKSLASH => ImGuiKey.Backslash,
            _ => ImGuiKey.None
        };

        return result != ImGuiKey.None;
    }

    private void UpdateImGuiInput(SDLUtil.InputSnapshot snapshot)
    {
        ImGuiIOPtr io = ImGui.GetIO();
        var mousePos = snapshot.MousePosition * backend.WindowPixelDensity;
        io.AddMousePosEvent(mousePos.X, mousePos.Y);
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

                // Submit modifier keys
                switch (imguikey)
                {
                    case ImGuiKey.LeftCtrl:
                        io.AddKeyEvent(ImGuiKey.ModCtrl, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.RightCtrl));
                        break;
                    case ImGuiKey.RightCtrl:
                        io.AddKeyEvent(ImGuiKey.ModCtrl, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.LeftCtrl));
                        break;
                    case ImGuiKey.LeftShift:
                        io.AddKeyEvent(ImGuiKey.ModShift, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.RightShift));
                        break;
                    case ImGuiKey.RightShift:
                        io.AddKeyEvent(ImGuiKey.ModShift, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.LeftShift));
                        break;
                    case ImGuiKey.LeftAlt:
                        io.AddKeyEvent(ImGuiKey.ModAlt, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.RightAlt));
                        break;
                    case ImGuiKey.RightAlt:
                        io.AddKeyEvent(ImGuiKey.ModAlt, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.LeftAlt));
                        break;
                    case ImGuiKey.LeftSuper:
                        io.AddKeyEvent(ImGuiKey.ModSuper, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.RightSuper));
                        break;
                    case ImGuiKey.RightSuper:
                        io.AddKeyEvent(ImGuiKey.ModSuper, keyEvent.Down || ImGui.IsKeyDown(ImGuiKey.LeftSuper));
                        break;
                }
            }
        }

        if (io.WantTextInput != textInputActive)
        {
            if (io.WantTextInput)
                Backend.BeginTextInput();
            else
                Backend.StopTextInput();

            textInputActive = io.WantTextInput;
        }
    }

    private unsafe void RenderImDrawData(ImDrawDataPtr draw_data)
    {
        uint vertexOffsetInVertices = 0;
        uint indexOffsetInElements = 0;

        if (draw_data.CmdLists.Size == 0)
        {
            return;
        }

        cl.UpdateBuffer(textureGlobalsBuffer, 0, textureGlobalsStruct);

        // Update vertex, index buffers

        uint totalVBSize = (uint)(draw_data.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>());
        if (totalVBSize > vertexBuffer.SizeInBytes)
        {
            gd.DisposeWhenIdle(vertexBuffer);
            vertexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalVBSize * 1.5f), BufferUsage.VertexBuffer | BufferUsage.Dynamic));
        }

        uint totalIBSize = (uint)(draw_data.TotalIdxCount * sizeof(ushort));
        if (totalIBSize > indexBuffer.SizeInBytes)
        {
            gd.DisposeWhenIdle(indexBuffer);
            indexBuffer = gd.ResourceFactory.CreateBuffer(new BufferDescription((uint)(totalIBSize * 1.5f), BufferUsage.IndexBuffer | BufferUsage.Dynamic));
        }

        for (int i = 0; i < draw_data.CmdLists.Size; i++)
        {
            ImDrawListPtr cmd_list = draw_data.CmdLists[i];

            cl.UpdateBuffer(
                vertexBuffer,
                vertexOffsetInVertices * (uint)Unsafe.SizeOf<ImDrawVert>(),
                ref *cmd_list.VtxBuffer.Data,
                (uint)(cmd_list.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>()));

            cl.UpdateBuffer(
                indexBuffer,
                indexOffsetInElements * sizeof(ushort),
                ref *cmd_list.IdxBuffer.Data,
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

        cl.UpdateBuffer(projMatrixBuffer, 0, ref mvp);

        cl.SetVertexBuffer(0, vertexBuffer);
        cl.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
        cl.SetPipeline(pipeline);
        cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
        cl.SetGraphicsResourceSet(0, mainResourceSet);

        draw_data.ScaleClipRects(io.DisplayFramebufferScale);

        // Render textures
        if (!Unsafe.IsNullRef(ref draw_data.Textures))
        {
            for (int i = 0; i < draw_data.Textures.Size; i++)
            {
                ImTextureDataPtr tex = draw_data.Textures[i];
                if (tex.Status != ImTextureStatus.Ok)
                    UpdateTexture(tex);
            }
        }
        // Render command lists
        int vtx_offset = 0;
        int idx_offset = 0;
        for (int n = 0; n < draw_data.CmdLists.Size; n++)
        {
            ImDrawListPtr cmd_list = draw_data.CmdLists[n];
            for (int cmd_i = 0; cmd_i < cmd_list.CmdBuffer.Size; cmd_i++)
            {
                ImDrawCmd pcmd = cmd_list.CmdBuffer[cmd_i];
                nint userCallbackData = (nint)pcmd.UserCallbackData;
                if (pcmd.UserCallback != (void*)0)
                {
                    // This doesn't play nice with Vulkan. I think it's because I'm not supposed to
                    // use UpdateBuffer in the middle of a render pass?
                    if (gd.BackendType != GraphicsBackend.Vulkan)
                    {
                        var callback = (delegate*<ImDrawList*, ImDrawCmd*, void>)pcmd.UserCallback;
                        callback((ImDrawList*)cmd_list, &pcmd);
                    }

                    Marshal.FreeHGlobal(userCallbackData);
                }
                else
                {
                    if (pcmd.GetTexID() != IntPtr.Zero)
                    {
                        var rsi = GetTextureResourceSetInfo(pcmd.GetTexID());
                        cl.SetGraphicsResourceSet(1, rsi.ResourceSet);
                    }

                    // Vulkan crashes if we send rect values < 0, and apparently imgui does this
                    // sometimes (when modal windows are displayed). Not sure why, but we fix it
                    // with Max(0, ...).
                    cl.SetScissorRect(
                        0,
                        (uint)Math.Max(0, pcmd.ClipRect.X),
                        (uint)Math.Max(0, pcmd.ClipRect.Y),
                        (uint)(pcmd.ClipRect.Z - pcmd.ClipRect.X),
                        (uint)(pcmd.ClipRect.W - pcmd.ClipRect.Y));

                    cl.DrawIndexed(pcmd.ElemCount, 1, pcmd.IdxOffset + (uint)idx_offset, (int)pcmd.VtxOffset + vtx_offset, 0);
                }
            }
            vtx_offset += cmd_list.VtxBuffer.Size;
            idx_offset += cmd_list.IdxBuffer.Size;
        }
    }

    /// <summary>
    /// Callback function used by all ImGui render callbacks. These callbacks are invoked from
    /// RenderImDrawData above.
    /// </summary>
    public static unsafe void RenderCallback(ImDrawList* parentList, ImDrawCmd* cmd)
    {
        var data = *(LynnaLab.ImGuiX.RenderCallbackData*)cmd->UserCallbackData;
        var controller = (ImGuiController)GCHandle.FromIntPtr(data.Target).Target;
        switch (data.Type)
        {
            case ImGuiXCallback.SetAlpha:
                controller.textureGlobalsStruct.alpha = data.Alpha;
                controller.cl.UpdateBuffer(controller.textureGlobalsBuffer, 0, controller.textureGlobalsStruct);
                break;
            case ImGuiXCallback.SetInterpolation:
                controller.textureGlobalsStruct.InterpolationMode = data.Interpolation;
                controller.cl.UpdateBuffer(controller.textureGlobalsBuffer, 0, controller.textureGlobalsStruct);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Frees all graphics resources used by the renderer.
    /// </summary>
    public void Dispose()
    {
        vertexBuffer.Dispose();
        indexBuffer.Dispose();
        projMatrixBuffer.Dispose();
        layout.Dispose();
        textureLayout.Dispose();
        pipeline.Dispose();
        renderTilesetPipeline.Dispose();
        mainResourceSet.Dispose();

        foreach (IDisposable resource in ownedResources)
        {
            resource.Dispose();
        }

        if (selfHandle.IsAllocated)
            selfHandle.Free();
    }

    void SetupRenderTilesetPipeline(OutputDescription outputDescription)
    {
        Shader vertexShader = LoadShader(gd.ResourceFactory, "tileset-vertex", ShaderStages.Vertex);
        Shader fragmentShader = LoadShader(gd.ResourceFactory, "tileset-frag", ShaderStages.Fragment);

        VertexLayoutDescription[] vertexLayouts = new VertexLayoutDescription[]
        {
                new VertexLayoutDescription(
                    new VertexElementDescription("in_position", VertexElementSemantic.Position, VertexElementFormat.Float2))
        };

        var shaderSet = new ShaderSetDescription(vertexLayouts, new[] { vertexShader, fragmentShader });

        // Layout for rendering a tileset
        renderTilesetLayout = gd.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
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
            ResourceLayouts = new[] { layout, renderTilesetLayout },
            ShaderSet = shaderSet,
            Outputs = outputDescription,
            ResourceBindingModel = ResourceBindingModel.Default
        };

        renderTilesetPipeline = gd.ResourceFactory.CreateGraphicsPipeline(ref pipelineDesc);

        gd.UpdateBuffer(renderTilesetVertexBuffer, 0, new Vector2[] {
            new Vector2(-1.0f, -1.0f),
            new Vector2(1.0f, -1.0f),
            new Vector2(-1.0f, 1.0f),
            new Vector2(1.0f, 1.0f)
        });

        ownedResources.Add(vertexShader);
        ownedResources.Add(fragmentShader);
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
        cl.SetGraphicsResourceSet(0, mainResourceSet);
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

            uint height = 0x800;

            TextureDescription textureDescription = TextureDescription.Texture2D(
                (uint)1,
                (uint)height,
                mipLevels: 1,
                arrayLayers: 1,
                PixelFormat.R16_UInt,
                TextureUsage.Sampled);

            tilesetGfxTexture = gd.ResourceFactory.CreateTexture(ref textureDescription);
            gd.UpdateTexture(tilesetGfxTexture, (nint)(ptr + 0x800), 0x1000, 0, 0, 0,
                             1, height, 1, 0, 0);
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

            var texture = gd.ResourceFactory.CreateTexture(ref textureDescription);
            gd.UpdateTexture(texture, data, 0, 0, 0,
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

        var resourceSet = gd.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(renderTilesetLayout,
                                       tilesetGfxTexture,
                                       tilesetMapTexture,
                                       tilesetFlagsTexture,
                                       tilesetPalette.PaletteTexture,
                                       gd.PointSampler));

        cl.SetGraphicsResourceSet(1, resourceSet);

        cl.Draw(4, 1, 0, 0);

        // Would be more optimal to cache and reuse these when possible
        gd.DisposeWhenIdle(tilesetGfxTexture);
        gd.DisposeWhenIdle(tilesetMapTexture);
        gd.DisposeWhenIdle(tilesetFlagsTexture);
        gd.DisposeWhenIdle(tilesetPalette);
        gd.DisposeWhenIdle(resourceSet);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Render a texture with pixel data provided by ImGui
    /// </summary>
    unsafe void UpdateTexture(ImTextureDataPtr tex)
    {
        if (tex.Format != ImTextureFormat.Rgba32)
            throw new NotImplementedException($"Unsupported ImGui texture format: {tex.Format}");

        if (tex.Status == ImTextureStatus.WantCreate)
        {
            Texture texture = gd.ResourceFactory.CreateTexture(TextureDescription.Texture2D(
                (uint)tex.Width,
                (uint)tex.Height,
                1,
                1,
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureUsage.Sampled));

            gd.UpdateTexture(
                texture,
                (nint)tex.GetPixels(),
                (uint)(tex.Width * tex.Height * 4),
                0,
                0,
                0,
                (uint)tex.Width,
                (uint)tex.Height,
                1,
                0,
                0);

            var resourceSet = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                textureLayout,
                texture,
                gd.PointSampler,
                texture,
                gd.LinearSampler,
                fontFragBuffer,
                fullSourceViewportBuffer));

            IntPtr id = GetNextImGuiBindingID();
            viewsById.Add(id, new ResourceSetInfo(id, resourceSet, null));
            ownedResources.Add(texture);
            ownedResources.Add(resourceSet);

            tex.SetTexID(id);
            tex.SetStatus(ImTextureStatus.Ok);
            tex.BackendUserData = (void*)GCHandle.ToIntPtr(GCHandle.Alloc(texture));
        }
        if (tex.Status == ImTextureStatus.WantUpdates)
        {
            Texture texture = (Texture)GCHandle.FromIntPtr((IntPtr)tex.BackendUserData).Target;

            // Veldrid does not support copying a texture from a byte array with a particular pitch,
            // instead we must pack the data into an array first.
            byte[] packed = new byte[tex.UpdateRect.W * tex.UpdateRect.H * tex.BytesPerPixel];
            fixed (byte* dest = packed)
            {
                for (int y = 0; y < tex.UpdateRect.H; y++)
                {
                    Buffer.MemoryCopy(tex.GetPixelsAt(tex.UpdateRect.X, tex.UpdateRect.Y + y),
                        dest + y * tex.UpdateRect.W * tex.BytesPerPixel,
                        tex.UpdateRect.W * tex.BytesPerPixel,
                        tex.UpdateRect.W * tex.BytesPerPixel);
                }
                gd.UpdateTexture(
                    texture,
                    (nint)dest,
                    (uint)(tex.UpdateRect.W * tex.UpdateRect.H * 4),
                    (uint)tex.UpdateRect.X,
                    (uint)tex.UpdateRect.Y,
                    0,
                    (uint)tex.UpdateRect.W,
                    (uint)tex.UpdateRect.H,
                    1,
                    0,
                    0);
            }

            tex.SetStatus(ImTextureStatus.Ok);
        }
        if (tex.Status == ImTextureStatus.WantDestroy && tex.UnusedFrames > 0)
        {
            IntPtr id = (IntPtr)tex.TexID;
            if (viewsById.TryGetValue(id, out ResourceSetInfo rsi))
            {
                ownedResources.Remove(rsi.ResourceSet);
                gd.DisposeWhenIdle(rsi.ResourceSet);
                viewsById.Remove(id);
            }

            GCHandle handle = GCHandle.FromIntPtr((IntPtr)tex.BackendUserData);
            Texture texture = (Texture)handle.Target;
            ownedResources.Remove(texture);
            gd.DisposeWhenIdle(texture);
            handle.Free();
            tex.BackendUserData = null;

            tex.SetTexID(IMTEXTURE_INVALID);
            tex.SetStatus(ImTextureStatus.Destroyed);
        }
    }


    /// <summary>
    /// ResourceSetInfo: Identifies a texture with ImGui and Veldrid bindings.
    /// </summary>
    private struct ResourceSetInfo
    {
        public readonly IntPtr ImGuiBinding;
        public readonly ImTextureRef TextureRef;
        public readonly ResourceSet ResourceSet;
        public readonly VeldridTextureBase Texture;

        public unsafe ResourceSetInfo(IntPtr imGuiBinding, ResourceSet resourceSet, VeldridTextureBase texture)
        {
            ImGuiBinding = imGuiBinding;
            ResourceSet = resourceSet;
            Texture = texture;

            TextureRef = new(texId: ImGuiBinding);
        }
    }
}
