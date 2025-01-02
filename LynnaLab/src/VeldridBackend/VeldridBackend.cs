using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

using IBackend = LynnaLab.IBackend;
using Interpolation = LynnaLab.Interpolation;
using Image = LynnaLab.Image;
using System.IO;

namespace VeldridBackend;

public class VeldridBackend : IBackend
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public VeldridBackend(string title)
    {
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
        _controller = new ImGuiController(this, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
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

    // ================================================================================
    // Public methods
    // ================================================================================

    public void HandleEvents(float deltaTime)
    {
        InputSnapshot snapshot = _window.PumpEvents();

        if (!_window.Exists)
            return;

        // Feed the input events to our ImGui controller, which passes them through to ImGui.
        _controller.Update(deltaTime, snapshot);
    }

    public void Render()
    {
        _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
        _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
        _controller.Render(_gd, _cl);
        _cl.End();

        _gd.SubmitCommands(_cl);
        _gd.SwapBuffers(_gd.MainSwapchain);

        _cl.Begin();
    }

    public void Close()
    {
        forceWindowClose = true;
        _window.Close();
    }

    public Image ImageFromBitmap(Bitmap bitmap)
    {
        return new VeldridImage(_controller, bitmap, 1.0f);
    }
    public Image ImageFromFile(string filename)
    {
        Bitmap bitmap = new Bitmap(filename);
        Image image = ImageFromBitmap(bitmap);
        bitmap.Dispose();
        return image;
    }
    public Image CreateImage(int width, int height)
    {
        return new VeldridImage(_controller, width, height, 1.0f);
    }

    public void RecreateFontTexture()
    {
        // This may be overkill, but the commented line below doesn't work for some reason
        _controller.CreateDeviceResources(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription);
        //_controller.RecreateFontDeviceTexture(_gd);
    }

    public void SetIcon(string path)
    {
        ApplicationIcon.SetWindowIcon(_window.SdlWindowHandle, path);
    }


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
