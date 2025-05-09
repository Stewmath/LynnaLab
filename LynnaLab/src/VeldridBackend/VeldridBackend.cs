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
        #if RENDERDOC
        Veldrid.RenderDoc.Load(out rd);
        #endif

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(50, 50, 1280, 720, WindowState.Maximized, title),
            new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            GraphicsBackend.OpenGL,
            out window,
            out gd);

        window.Resized += () =>
        {
            gd.MainSwapchain.Resize((uint)window.Width, (uint)window.Height);
            controller.WindowResized(window.Width, window.Height);
        };

        window.SetCloseRequestedHandler(CloseRequestedHandler);

        cl = gd.ResourceFactory.CreateCommandList();
        cl.Begin();
        controller = new ImGuiController(this, cl, gd.MainSwapchain.Framebuffer.OutputDescription, window.Width, window.Height);

        GreyscalePalette = new VeldridPalette(controller, GbGraphics.GrayPalette, transparentIndex: -1);
    }

    ~VeldridBackend()
    {
        // Clean up Veldrid resources
        gd.WaitForIdle();
        controller.Dispose();
        cl.Dispose();
        gd.Dispose();
    }

    // ================================================================================
    // Variables
    // ================================================================================
    #if RENDERDOC
    Veldrid.RenderDoc rd;
    #endif

    static Sdl2Window window;
    static GraphicsDevice gd;
    static CommandList cl;
    static ImGuiController controller;
    static Vector3 clearColor = new Vector3(0.45f, 0.55f, 0.6f);
    bool forceWindowClose;


    // ================================================================================
    // Properties
    // ================================================================================
    public bool Exited { get { return !window.Exists; } }
    public bool CloseRequested { get; set; }
    public GraphicsDevice GraphicsDevice { get { return gd; } }
    public CommandList CommandList { get { return cl; } }

    internal VeldridPalette GreyscalePalette { get; private set; }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Called before ImGui rendering occurs
    /// </summary>
    public void HandleEvents(float deltaTime)
    {
        InputSnapshot snapshot = window.PumpEvents();

        if (!window.Exists)
            return;

        // Feed the input events to our ImGui controller, which passes them through to ImGui.
        controller.Update(deltaTime, snapshot);
    }

    /// <summary>
    /// Called after main imgui rendering occurs, this will draw the results of that
    /// </summary>
    public void Render()
    {
        cl.SetFramebuffer(gd.MainSwapchain.Framebuffer);
        cl.ClearColorTarget(0, new RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, 1f));
        controller.Render();
        cl.End();

        gd.SubmitCommands(cl);
        gd.SwapBuffers(gd.MainSwapchain);

        #if RENDERDOC
        if (rd != null && rd.IsFrameCapturing())
            rd.EndFrameCapture();
        #endif

        cl.Begin();
    }

    /// <summary>
    /// Closes the window.
    /// </summary>
    public void Close()
    {
        forceWindowClose = true;
        window.Close();
    }

    /// <summary>
    /// Creates an image from the given bitmap. The image tracks changes from the bitmap as long as
    /// the bitmap is not Disposed.
    /// </summary>
    public VeldridRgbaTexture TextureFromBitmap(Bitmap bitmap)
    {
        return new VeldridRgbaTexture(controller, bitmap);
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
        return new VeldridRgbaTexture(controller, width, height, renderTarget);
    }

    public VeldridTextureWindow CreateTextureWindow(RgbaTexture source, Point topLeft, Point size)
    {
        return new VeldridTextureWindow(controller, source, topLeft, size);
    }

    public void RenderTileset(RgbaTexture dest, Tileset tileset)
    {
        controller.RenderTileset(dest, tileset);
    }

    public void RecreateFontTexture()
    {
        controller.RecreateFontDeviceTexture();
    }

    public void SetIcon(string path)
    {
        ApplicationIcon.SetWindowIcon(window.SdlWindowHandle, path);
    }

    #if RENDERDOC
    public void TriggerRenderDocCapture()
    {
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
