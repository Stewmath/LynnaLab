using SDL;
using static SDL.SDL3;

namespace SDLUtil;

/// <summary>
/// Wrapper over SDL_Window, exposing functionality in a saner, C# friendly way.
/// </summary>
public unsafe class SDLWindow
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public SDLWindow(string title, int width, int height, SDL.SDL_WindowFlags flags)
    {
        SDL_SetHint(SDL_HINT_MOUSE_FOCUS_CLICKTHROUGH, "1");

        SDL_DisplayID display = SDL_GetPrimaryDisplay();
        if (display != 0)
        {
            float scale = SDL_GetDisplayContentScale(display);
            width = (int)(width * scale);
            height = (int)(height * scale);
        }

        Handle = SDL_CreateWindow(title, width, height, flags);

        if ((nint)Handle == 0)
            throw new Exception("SDL_CreateWindow error");

        if (!SDL_ShowWindow(Handle))
            throw new Exception("SDL_ShowWindow error");

        UpdateSize();

        Exists = true;
    }

    // ================================================================================
    // Variables
    // ================================================================================

    Func<bool> closeRequestedHandler;

    // ================================================================================
    // Properties
    // ================================================================================

    public SDL_Window* Handle { get; private set; }

    /// <summary>
    /// Size of window in system-native coordinates. May not be the same as SizeInPixels.
    /// </summary>
    public Point Size { get; private set; }

    /// <summary>
    /// Size of window in pixels. See SDL_GetWindowPixelDensity().
    /// </summary>
    public Point SizeInPixels { get; private set; }

    public bool Exists { get; private set; }

    // ================================================================================
    // Events
    // ================================================================================

    // Parameter is true if display scale changed
    public event Action<bool> Resized;

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Set a function to call to intercept window close events.
    /// </summary>
    public void SetCloseRequestedHandler(Func<bool> handler)
    {
        closeRequestedHandler = handler;
    }

    /// <summary>
    /// Handle SDL events, returns an InputSnapshot representing new inputs.
    /// </summary>
    public InputSnapshot PumpEvents()
    {
        SDLInputSnapshot snapshot = new();

        float mouseX, mouseY;
        snapshot.mouseButtonFlags = SDL_GetMouseState(&mouseX, &mouseY);
        snapshot.MousePosition = new Vector2(mouseX, mouseY);

        var keyPresses = new List<uint>();
        var keyEvents = new List<KeyEvent>();

        bool updateSize = false;
        bool updateScale = false;

        SDL_Event ev;
        while (SDL_PollEvent(&ev))
        {
            switch (ev.Type)
            {
                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
                case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
                    break;
                case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
                    snapshot.WheelDelta += ev.wheel.y;
                    break;
                case SDL_EventType.SDL_EVENT_KEY_DOWN:
                case SDL_EventType.SDL_EVENT_KEY_UP:
                    keyEvents.Add(new KeyEvent(ev.key.key, ev.key.down));
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_CLOSE_REQUESTED:
                    Close();
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_RESIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_RESTORED:
                    updateSize = true;
                    break;
                case SDL_EventType.SDL_EVENT_WINDOW_DISPLAY_SCALE_CHANGED:
                case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                    updateSize = true;
                    updateScale = true;
                    break;
                default:
                    //Console.WriteLine($"EVENT: {ev.Type}");
                    break;
            }
        }

        if (updateSize)
        {
            UpdateSize();
            Resized?.Invoke(updateScale);
        }

        snapshot.KeyCharPresses = keyPresses;
        snapshot.KeyEvents = keyEvents;

        return snapshot;
    }

    /// <summary>
    /// Close the window (or call the close handler first if set with SetCloseRequestedHandler).
    /// </summary>
    public void Close()
    {
        if (closeRequestedHandler?.Invoke() ?? false)
        {
            return;
        }

        SDL_DestroyWindow(Handle);
        Exists = false;
    }

    /// <summary>
    /// Set the icon from a BMP file in the given path.
    /// </summary>
    public void SetIcon(string path)
    {
        SDL_Surface *surface = SDL_LoadBMP(path);
        if (surface == null)
            return;
        SDL_SetWindowIcon(Handle, surface);
        SDL_DestroySurface(surface);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    unsafe void UpdateSize()
    {
        int w, h;
        SDL_GetWindowSize(Handle, &w, &h);
        Size = new Point(w, h);
        SDL_GetWindowSizeInPixels(Handle, &w, &h);
        SizeInPixels = new Point(w, h);
    }
}
