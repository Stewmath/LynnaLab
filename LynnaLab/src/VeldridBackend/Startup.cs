using Veldrid;
using static SDL.SDL3;
using SDL;
using SDLUtil;

namespace VeldridBackend;

/// <summary>
/// Helper functions for setting up Veldrid with SDL3. Similar to Veldrid.StartupUtilities but for
/// SDL3 instead of SDL2.
/// </summary>
public static class Startup
{
    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Create an SDLWindow and a Veldrid GraphicsDevice attached to that window.
    /// </summary>
    public static void CreateWindowAndGraphicsDevice(
        string title,
        int width,
        int height,
        GraphicsDeviceOptions deviceOptions,
        GraphicsBackend backend,
        out SDLWindow window,
        out GraphicsDevice gd)
    {
        string preferredDrivers = null;
        bool videoInitialized = false;

        // On Linux, prefer wayland if available. (X11+Vulkan has a crashing bug when resizing the
        // window. Probably my fault somehow, but I couldn't figure it out.)
        if (SDL_GetPlatform() == "Linux")
            preferredDrivers = "wayland";

        if (preferredDrivers != null)
        {
            SDL_SetHint(SDL_HINT_VIDEO_DRIVER, preferredDrivers);
            videoInitialized = SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO);
        }

        if (!videoInitialized)
        {
            SDL_ResetHint(SDL_HINT_VIDEO_DRIVER);
            if (!(videoInitialized = SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_VIDEO)))
                throw new Exception($"SDL failed to initialize: {SDL_GetError()}.");
        }

        SDL_WindowFlags flags =
            SDL_WindowFlags.SDL_WINDOW_RESIZABLE
            | SDL_WindowFlags.SDL_WINDOW_MAXIMIZED;

        if (backend == GraphicsBackend.OpenGL || backend == GraphicsBackend.OpenGLES)
            flags |= SDL_WindowFlags.SDL_WINDOW_OPENGL;

        // Don't add SDL_WINDOW_VULKAN to flags because we don't initialize vulkan through SDL.
        // Veldrid already knows how to set it up and attach it to the window on each platform.

        window = new SDLWindow(title, width, height, flags);

        gd = CreateGraphicsDevice(window, deviceOptions, backend);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    static GraphicsDevice CreateGraphicsDevice(SDLWindow window, GraphicsDeviceOptions deviceOptions, GraphicsBackend backend)
    {
        switch (backend)
        {
            case GraphicsBackend.Vulkan:
                return CreateVulkanGraphicsDevice(deviceOptions, window, false);
            case GraphicsBackend.OpenGL:
                return CreateOpenGLGraphicsDevice(deviceOptions, window, backend);
            default:
                throw new Exception($"Veldrid backend '{backend}' not supported.");
        }
    }

    // ================================================================================
    // Vulkan
    // ================================================================================

    static unsafe GraphicsDevice CreateVulkanGraphicsDevice(
        GraphicsDeviceOptions options,
        SDLWindow window,
        bool colorSrgb)
    {
        SwapchainDescription scDesc = new SwapchainDescription(
            GetSwapchainSource(window),
            (uint)window.Width,
            (uint)window.Height,
            options.SwapchainDepthFormat,
            options.SyncToVerticalBlank,
            colorSrgb);
        GraphicsDevice gd = GraphicsDevice.CreateVulkan(options, scDesc);

        return gd;
    }

    /// <summary>
    /// Read the window properties required on each platform to attach the vulkan/opengl/whatever
    /// instance to the window.
    /// </summary>
    static unsafe SwapchainSource GetSwapchainSource(SDLWindow window)
    {
        SDL_PropertiesID props = SDL_GetWindowProperties(window.Handle);
        string videoDriver = SDL_GetCurrentVideoDriver();

        switch (videoDriver)
        {
            case "windows":
                nint win_hwnd = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WIN32_HWND_POINTER, 0);
                nint win_inst = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WIN32_INSTANCE_POINTER, 0);
                if (win_hwnd != 0 && win_inst != 0)
                    return SwapchainSource.CreateWin32(win_hwnd, win_inst);
                else
                    throw new Exception($"Invalid windows hwnd/instance: {win_hwnd},{win_inst}");
            case "x11":
                nint xdisplay = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_X11_DISPLAY_POINTER, 0);
                nint xwindow = (nint)SDL_GetNumberProperty(props, SDL_PROP_WINDOW_X11_WINDOW_NUMBER, 0);
                if (xdisplay != 0 && xwindow != 0)
                    return SwapchainSource.CreateXlib(xdisplay, xwindow);
                else
                    throw new Exception($"Invalid X11 display/window: {xdisplay},{xwindow}");
            case "wayland":
                nint wdisplay = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WAYLAND_DISPLAY_POINTER, 0);
                nint wsurface = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_WAYLAND_SURFACE_POINTER, 0);
                if (wdisplay != 0 && wsurface != 0)
                    return SwapchainSource.CreateWayland(wdisplay, wsurface);
                else
                    throw new Exception($"Invalid wayland display/surface: {wdisplay},{wsurface}");
            case "cocoa": // Mac (UNTESTED)
                nint mac_window = SDL_GetPointerProperty(props, SDL_PROP_WINDOW_COCOA_WINDOW_POINTER, 0);
                if (mac_window != 0)
                    return SwapchainSource.CreateNSWindow(mac_window);
                else
                    throw new Exception($"Invalid cocoa window: {mac_window}");
            default:
                throw new PlatformNotSupportedException("Cannot create a SwapchainSource for " + videoDriver + ".");
        }
    }

    // ================================================================================
    // OpenGL
    // ================================================================================

    private static readonly object s_glVersionLock = new object();
    private static (int Major, int Minor)? s_maxSupportedGLVersion;
    private static (int Major, int Minor)? s_maxSupportedGLESVersion;

    public static unsafe GraphicsDevice CreateOpenGLGraphicsDevice(
        GraphicsDeviceOptions options,
        SDLWindow window,
        GraphicsBackend backend)
    {
        SetSDLGLContextAttributes(options, backend);

        SDL_GLContextState* contextHandle = SDL_GL_CreateContext(window.Handle);
        if (contextHandle == null)
        {
            string errorString = SDL_GetError();
            throw new VeldridException(
                $"Unable to create OpenGL Context: \"{errorString}\". This may indicate that the system does not support the requested OpenGL profile, version, or Swapchain format.");
        }

        int actualDepthSize;
        int actualStencilSize;
        SDL_GL_GetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, &actualDepthSize);
        SDL_GL_GetAttribute(SDL_GLAttr.SDL_GL_STENCIL_SIZE, &actualStencilSize);

        SDL_GL_SetSwapInterval(options.SyncToVerticalBlank ? 1 : 0);

        Veldrid.OpenGL.OpenGLPlatformInfo platformInfo = new Veldrid.OpenGL.OpenGLPlatformInfo(
            (nint)contextHandle,
            func => SDL_GL_GetProcAddress(func),
            context => SDL_GL_MakeCurrent(window.Handle, (SDL_GLContextState*)context),
            () => (nint)SDL_GL_GetCurrentContext(),
            () => SDL_GL_MakeCurrent(null, null),
            context => SDL_GL_DestroyContext((SDL_GLContextState*)context),
            () => SDL_GL_SwapWindow(window.Handle),
            sync => SDL_GL_SetSwapInterval(sync ? 1 : 0));

        return GraphicsDevice.CreateOpenGL(
            options,
            platformInfo,
            (uint)window.Width,
            (uint)window.Height);
    }

    public static unsafe void SetSDLGLContextAttributes(GraphicsDeviceOptions options, GraphicsBackend backend)
    {
        if (backend != GraphicsBackend.OpenGL && backend != GraphicsBackend.OpenGLES)
        {
            throw new VeldridException(
                $"{nameof(backend)} must be {nameof(GraphicsBackend.OpenGL)} or {nameof(GraphicsBackend.OpenGLES)}.");
        }

        SDL_GLContextFlag contextFlags = options.Debug
            ? SDL_GLContextFlag.SDL_GL_CONTEXT_DEBUG_FLAG | SDL_GLContextFlag.SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG
            : SDL_GLContextFlag.SDL_GL_CONTEXT_FORWARD_COMPATIBLE_FLAG;

        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_FLAGS, (int)contextFlags);

        (int major, int minor) = GetMaxGLVersion(backend == GraphicsBackend.OpenGLES);

        if (backend == GraphicsBackend.OpenGL)
        {
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_CORE);
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, minor);
        }
        else
        {
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, (int)SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_ES);
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, minor);
        }

        int depthBits = 0;
        int stencilBits = 0;
        if (options.SwapchainDepthFormat.HasValue)
        {
            switch (options.SwapchainDepthFormat)
            {
                case PixelFormat.R16_UNorm:
                    depthBits = 16;
                    break;
                case PixelFormat.D24_UNorm_S8_UInt:
                    depthBits = 24;
                    stencilBits = 8;
                    break;
                case PixelFormat.R32_Float:
                    depthBits = 32;
                    break;
                case PixelFormat.D32_Float_S8_UInt:
                    depthBits = 32;
                    stencilBits = 8;
                    break;
                default:
                    throw new VeldridException("Invalid depth format: " + options.SwapchainDepthFormat.Value);
            }
        }

        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_DEPTH_SIZE, depthBits);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_STENCIL_SIZE, stencilBits);

        if (options.SwapchainSrgbFormat)
        {
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_FRAMEBUFFER_SRGB_CAPABLE, 1);
        }
        else
        {
            SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_FRAMEBUFFER_SRGB_CAPABLE, 0);
        }
    }

    private static (int Major, int Minor) GetMaxGLVersion(bool gles)
    {
        lock (s_glVersionLock)
        {
            (int Major, int Minor)? maxVer = gles ? s_maxSupportedGLESVersion : s_maxSupportedGLVersion;
            if (maxVer == null)
            {
                maxVer = TestMaxVersion(gles);
                if (gles) { s_maxSupportedGLESVersion = maxVer; }
                else { s_maxSupportedGLVersion = maxVer; }
            }

            return maxVer.Value;
        }
    }

    private static (int Major, int Minor) TestMaxVersion(bool gles)
    {
        (int, int)[] testVersions = gles
            ? new[] { (3, 2), (3, 0) }
            : new[] { (4, 6), (4, 3), (4, 0), (3, 3), (3, 0) };

        foreach ((int major, int minor) in testVersions)
        {
            if (TestIndividualGLVersion(gles, major, minor)) { return (major, minor); }
        }

        return (0, 0);
    }

    private static unsafe bool TestIndividualGLVersion(bool gles, int major, int minor)
    {
        SDL_GLProfile profileMask = gles ? SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_ES
            : SDL_GLProfile.SDL_GL_CONTEXT_PROFILE_CORE;

        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_PROFILE_MASK, (int)profileMask);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MAJOR_VERSION, major);
        SDL_GL_SetAttribute(SDL_GLAttr.SDL_GL_CONTEXT_MINOR_VERSION, minor);

        SDL_Window* window = SDL_CreateWindow(
            string.Empty,
            1, 1,
            SDL_WindowFlags.SDL_WINDOW_HIDDEN | SDL_WindowFlags.SDL_WINDOW_OPENGL);

        if (window == null)
        {
            Debug.WriteLine($"Unable to create version {major}.{minor} {profileMask} context.");
            return false;
        }

        SDL_GLContextState* context = SDL_GL_CreateContext(window);
        if (context == null)
        {
            Debug.WriteLine($"Unable to create version {major}.{minor} {profileMask} context.");
            SDL_DestroyWindow(window);
            return false;
        }

        Debug.WriteLine($"Created OpenGL context: v{major}.{minor}");

        SDL_GL_DestroyContext(context);
        SDL_DestroyWindow(window);
        return true;
    }
}
