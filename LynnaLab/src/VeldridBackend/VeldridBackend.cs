using System.Numerics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace VeldridBackend
{
    public class VeldridBackend : LynnaLab.IBackend
    {
        // ================================================================================
        // Constructors
        // ================================================================================
        public VeldridBackend()
        {
            // Create window, GraphicsDevice, and all resources necessary for the demo.
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(50, 50, 1280, 720, WindowState.Maximized, "LynnaLab"),
                new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
                out _window,
                out _gd);

            _window.Resized += () =>
            {
                _gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
                _controller.WindowResized(_window.Width, _window.Height);
            };

            _cl = _gd.ResourceFactory.CreateCommandList();
            _controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
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


        // ================================================================================
        // Properties
        // ================================================================================
        public bool Exited { get { return !_window.Exists; } }

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
            _cl.Begin();
            _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
            _controller.Render(_gd, _cl);
            _cl.End();
            _gd.SubmitCommands(_cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }

        public LynnaLab.Image ImageFromBitmap(LynnaLib.Bitmap bitmap)
        {
            return new VeldridImage(_controller, bitmap);
        }

        public void RecreateFontTexture()
        {
            // This may be overkill, but the commented line below doesn't work for some reason
            _controller.CreateDeviceResources(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription);
            //_controller.RecreateFontDeviceTexture(_gd);
        }


        // ================================================================================
        // Private methods
        // ================================================================================
    }
}