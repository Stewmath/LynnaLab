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
