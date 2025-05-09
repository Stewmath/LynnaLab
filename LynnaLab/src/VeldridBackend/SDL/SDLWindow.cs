using SDL;
using static SDL.SDL3;

namespace VeldridBackend;

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

    int width, height;
    Func<bool> closeRequestedHandler;

    // ================================================================================
    // Properties
    // ================================================================================

    public SDL_Window* Handle { get; private set; }

    public int Width { get { return width; } }
    public int Height { get { return height; } }
    public bool Exists { get; private set; }

    // ================================================================================
    // Events
    // ================================================================================

    public event Action Resized;

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
                case SDL_EventType.SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED:
                case SDL_EventType.SDL_EVENT_WINDOW_MINIMIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_MAXIMIZED:
                case SDL_EventType.SDL_EVENT_WINDOW_RESTORED:
                    UpdateSize();
                    Resized?.Invoke();
                    break;
                default:
                    //Console.WriteLine($"EVENT: {ev.Type}");
                    break;
            }
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
        width = w;
        height = h;

        //Console.WriteLine($"Size: {w},{h}");
    }
}
