using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

using Point = Util.Point;

namespace VeldridBackend;

/// <summary>
/// A class which acts as an interface to Veldrid-specific stuff. This used to implement an IBackend
/// interface as at extra layer of indirection but that was too much hassle with only one backend in
/// place. Still, consider this class as if it could be replaced by some other backend.
/// </summary>
public class VeldridBackend
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public VeldridBackend(string title)
    {
        #if DEBUG
        //Veldrid.RenderDoc.Load(out rd);
        #endif

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, 1280, 720, WindowState.Maximized, title),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            GraphicsBackend.OpenGL,
            out _window,
            out _gd);

        _window.Resized += () =>
        {
            _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
            _controller.WindowResized(_window.Width, _window.Height);
        };

        _window.SetCloseRequestedHandler(CloseRequestedHandler);

        _cl = _gd.ResourceFactory.CreateCommandList();
        _cl.Begin();
        _controller = new ImGuiController(this, _cl, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);

        GreyscalePalette = new VeldridPalette(_controller, GbGraphics.GrayPalette, transparentIndex: -1);
    }

    ~VeldridBackend()
    {
        // Clean up Veldrid resources
        _gd.WaitForIdle();
        _controller.Dispose();
        _cl.Dispose();
        _gd.Dispose();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    #if DEBUG
    Veldrid.RenderDoc rd;
    #endif

    static Sdl2Window _window;
    static GraphicsDevice _gd;
    static CommandList _cl;
    static ImGuiController _controller;
    static Vector3 _clearColor = new Vector3(0.45f, 0.55f, 0.6f);
    bool forceWindowClose;


    // ================================================================================
    // Properties
    // ================================================================================
    public bool Exited { get { return !_window.Exists; } }
    public bool CloseRequested { get; set; }
    public GraphicsDevice GraphicsDevice { get { return _gd; } }
    public CommandList CommandList { get { return _cl; } }

    internal VeldridPalette GreyscalePalette { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Called before ImGui rendering occurs
    /// </summary>
    public void HandleEvents(float deltaTime)
    {
        InputSnapshot snapshot = _window.PumpEvents();

        if (!_window.Exists)
            return;

        // Feed the input events to our ImGui controller, which passes them through to ImGui.
        _controller.Update(deltaTime, snapshot);
    }

    /// <summary>
    /// Called after main imgui rendering occurs, this will draw the results of that
    /// </summary>
    public void Render()
    {
        _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
        _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
        _controller.Render(_gd);
        _cl.End();

        _gd.SubmitCommands(_cl);
        _gd.SwapBuffers(_gd.MainSwapchain);

        #if DEBUG
        if (rd != null && rd.IsFrameCapturing())
            rd.EndFrameCapture();
        #endif

        _cl.Begin();
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    public void Close()
    {
        forceWindowClose = true;
        _window.Close();
    }

    /// <summary>
    /// Creates an image from the given bitmap. The image tracks changes from the bitmap as long as
    /// the bitmap is not Disposed.
    /// </summary>
    public VeldridRgbaTexture TextureFromBitmap(Bitmap bitmap)
    {
        return new VeldridRgbaTexture(_controller, bitmap);
    }
    public VeldridRgbaTexture TextureFromFile(string filename)
    {
        Bitmap bitmap = new Bitmap(filename);
        VeldridRgbaTexture tx = TextureFromBitmap(bitmap);
        bitmap.Dispose();
        return tx;
    }

    /// <summary>
    /// Creates a new RgbaTexture. User must ensure it's disposed at some point.
    /// </summary>
    public VeldridRgbaTexture CreateTexture(int width, int height, bool renderTarget = false)
    {
        return new VeldridRgbaTexture(_controller, width, height, renderTarget);
    }

    public VeldridTextureWindow CreateTextureWindow(RgbaTexture source, Point topLeft, Point size)
    {
        return new VeldridTextureWindow(_controller, source, topLeft, size);
    }

    public void RenderTileset(RgbaTexture dest, Tileset tileset)
    {
        _controller.RenderTileset(dest, tileset);
    }

    public void RecreateFontTexture()
    {
        _controller.RecreateFontDeviceTexture(_gd);
    }

    public void SetIcon(string path)
    {
        ApplicationIcon.SetWindowIcon(_window.SdlWindowHandle, path);
    }

    #if DEBUG
    public void TriggerRenderDocCapture()
    {
        Console.WriteLine("RenderDoc Capture");
        rd.TriggerCapture();
    }

    public void RenderDocUI()
    {
        rd.LaunchReplayUI();
    }

    public void BeginFrameCapture()
    {
        rd.StartFrameCapture();
    }

    public void EndFrameCapture()
    {
        rd.EndFrameCapture();
    }
    #endif


    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Called when the user closes the window. Returns true to suppress the window's closure.
    /// </summary>
    bool CloseRequestedHandler()
    {
        if (forceWindowClose)
            return false;
        CloseRequested = true;
        return true;
    }
}


public enum Interpolation
{
    Nearest = 0,
    Bicubic = 1,

    Count
}
