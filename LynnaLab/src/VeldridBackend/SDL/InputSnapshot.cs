using SDL;

namespace VeldridBackend;

public enum MouseButton
{
    Left,
    Right,
    Middle,
    Button1,
    Button2
}

public struct KeyEvent
{
    public KeyEvent(SDL_Keycode key, bool down)
    {
        this.Key = key;
        this.Down = down;
    }
    public SDL_Keycode Key;
    public bool Down;
}

/// <summary>
/// Interface to use for seeing what kinds of inputs have been received since last poll.
/// </summary>
public interface InputSnapshot
{
    // ================================================================================
    // Properties
    // ================================================================================

    public Vector2 MousePosition { get; }
    public float WheelDelta { get; }
    public IReadOnlyList<uint> KeyCharPresses { get; }
    public IReadOnlyList<KeyEvent> KeyEvents { get; }

    // ================================================================================
    // Public methods
    // ================================================================================

    public bool IsMouseDown(MouseButton button);
}

/// <summary>
/// Implementation of InputSnapshot for SDL.
/// </summary>
internal class SDLInputSnapshot : InputSnapshot
{
    public Vector2 MousePosition { get; set; }
    public float WheelDelta { get; set; }
    public IReadOnlyList<uint> KeyCharPresses { get; set; }
    public IReadOnlyList<KeyEvent> KeyEvents { get; set; }

    public SDL_MouseButtonFlags mouseButtonFlags;

    public bool IsMouseDown(MouseButton button)
    {
        SDL_MouseButtonFlags mask;
        switch (button)
        {
            case MouseButton.Left:
                mask = SDL_MouseButtonFlags.SDL_BUTTON_LMASK;
                break;
            case MouseButton.Right:
                mask = SDL_MouseButtonFlags.SDL_BUTTON_RMASK;
                break;
            case MouseButton.Middle:
                mask = SDL_MouseButtonFlags.SDL_BUTTON_MMASK;
                break;
            case MouseButton.Button1:
                mask = SDL_MouseButtonFlags.SDL_BUTTON_X1MASK;
                break;
            case MouseButton.Button2:
                mask = SDL_MouseButtonFlags.SDL_BUTTON_X2MASK;
                break;
            default:
                throw new Exception($"Unrecognized mouse button: {button}");
        }

        return (mouseButtonFlags & mask) != 0;
    }
}
