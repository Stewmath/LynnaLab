using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SDL;
using static SDL.SDL3;

namespace SDLUtil;

public static class SDLHelper
{
    // ================================================================================
    // Public methods
    // ================================================================================

    public static unsafe void ShowOpenFileDialog(SDLWindow window,
                                                 string location,
                                                 IReadOnlyList<(string name, string pattern)> filters,
                                                 Action<string> callback)
    {
        SDL_DialogFileFilter[] sfilters = new SDL_DialogFileFilter[filters.Count];

        for (int i=0; i<filters.Count; i++)
        {
            sfilters[i].name = (byte*)Marshal.StringToCoTaskMemUTF8(filters[i].name);
            sfilters[i].pattern = (byte*)Marshal.StringToCoTaskMemUTF8(filters[i].pattern);
        }

        GCHandle handle = GCHandle.Alloc(callback);

        fixed (SDL_DialogFileFilter* ptr = sfilters)
        {
            SDL_ShowOpenFileDialog(&OpenFileCallback, GCHandle.ToIntPtr(handle), window.Handle, ptr, filters.Count, location, false);
        }

        for (int i=0; i<filters.Count; i++)
        {
            Marshal.FreeCoTaskMem((nint)sfilters[i].name);
            Marshal.FreeCoTaskMem((nint)sfilters[i].pattern);
        }
    }

    public static unsafe void ShowOpenFolderDialog(SDLWindow window, string location, Action<string> callback)
    {
        GCHandle handle = GCHandle.Alloc(callback);
        SDL_ShowOpenFolderDialog(&OpenFileCallback, GCHandle.ToIntPtr(handle), window.Handle, location, false);
    }

    public static unsafe void RunOnMainThread(Action action)
    {
        GCHandle handle = GCHandle.Alloc(action);
        SDL_RunOnMainThread(&RunOnMainThreadCallback, GCHandle.ToIntPtr(handle), false);
    }

    /// <summary>
    /// Show a message box with the given buttons, returns the index of the button pressed.
    /// </summary>
    public static unsafe int ShowErrorMessageBox(string title, string message, IReadOnlyList<string> buttons)
    {
        SDL_MessageBoxData data;
        data.flags = SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR;
        data.window = null;
        data.title = (byte*)Marshal.StringToCoTaskMemUTF8(title);
        data.message = (byte*)Marshal.StringToCoTaskMemUTF8(message);
        data.numbuttons = buttons.Count;
        data.buttons = (SDL_MessageBoxButtonData*)Marshal.AllocCoTaskMem(sizeof(SDL_MessageBoxButtonData) * buttons.Count);
        data.colorScheme = null;

        for (int i=0; i<buttons.Count; i++)
        {
            data.buttons[i].flags = 0;
            data.buttons[i].buttonID = i;
            data.buttons[i].text = (byte*)Marshal.StringToCoTaskMemUTF8(buttons[i]);
        }

        int buttonid = 0;
        if (!SDL_ShowMessageBox(&data, &buttonid))
            throw new Exception($"SDL message box error: {SDL_GetError()}");

        for (int i=0; i<buttons.Count; i++)
            Marshal.FreeCoTaskMem((nint)data.buttons[i].text);

        Marshal.FreeCoTaskMem((nint)data.title);
        Marshal.FreeCoTaskMem((nint)data.message);
        Marshal.FreeCoTaskMem((nint)data.buttons);

        return buttonid;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private unsafe static void OpenFileCallback(nint userdata, byte** filelist, int filter)
    {
        if (filelist == null)
            throw new Exception($"SDL File dialog error: {SDL_GetError()}");

        string name = null;

        while (*filelist != null)
        {
            name = Marshal.PtrToStringUTF8((nint)(*filelist));
            filelist++;
            break; // Only expect one result
        }

        // Invoke callback
        RunOnMainThread(() =>
        {
            GCHandle handle = GCHandle.FromIntPtr(userdata);
            ((Action<string>)handle.Target)(name);
            handle.Free();
        });
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private unsafe static void RunOnMainThreadCallback(nint userdata)
    {
        GCHandle handle = GCHandle.FromIntPtr(userdata);
        ((Action)handle.Target)();
        handle.Free();
    }
}
