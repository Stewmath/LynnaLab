using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SameBoySharp;

public partial class SameBoy : IDisposable
{
    public SameBoy(Model model)
    {
        _gb = Native.GB_alloc();
        if (_gb == IntPtr.Zero)
            throw new InvalidOperationException("GB_alloc failed");

        _gb = Native.GB_init(_gb, model);
        if (_gb == IntPtr.Zero)
            throw new InvalidOperationException("GB_init failed");
    }


    private IntPtr _gb;
    private Native.GB_log_callback_t? _logCallback;

    // ================================================================================
    // Base
    // ================================================================================

    public ulong RunFrame()
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        return Native.GB_run_frame(_gb);
    }

    public void LoadBootRom(string path)
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        int error = Native.GB_load_boot_rom(_gb, path);
        if (error != 0)
            throw new Exception($"Error {error} loading boot ROM: " + path);
    }

    public void LoadRom(string path)
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        if (Native.GB_load_rom(_gb, path) != 0)
            throw new InvalidOperationException("Sameboy failed to load ROM: " + path);
    }

    // ================================================================================
    // Video
    // ================================================================================

    public IntPtr GetFrameBuffer()
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        return Native.GB_get_pixels_output(_gb);
    }

    // ================================================================================
    // Console I/O
    // ================================================================================

    public void SetLogCallback(Action<string> callback)
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        _logCallback = (gb, msg, attr) =>
        {
            string text = Marshal.PtrToStringAnsi(msg)!;
            callback(text);
        };

        Native.GB_set_log_callback(_gb, _logCallback);
    }

    // ================================================================================
    // Inspection
    // ================================================================================

    public uint GetRomCrc32()
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        return Native.GB_get_rom_crc32(_gb);
    }

    public string GetRomTitle()
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        var sb = new StringBuilder(17);
        Native.GB_get_rom_title(_gb, sb);
        return sb.ToString();
    }

    // ================================================================================
    // Debugger functions
    // ================================================================================

    /// <summary>
    /// Returns true on success (opposite of the real library!)
    ///
    /// result_bank = -1 if there is no bank number.
    /// </summary>
    public unsafe bool DebuggerEvaluate(string str, out ushort result, out short result_bank)
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        ushort r, rb;
        fixed (byte* utf8 = Encoding.UTF8.GetBytes(str))
        {
            bool retval = Native.GB_debugger_evaluate(_gb, utf8, &r, &rb);
            result = r;
            result_bank = (short)rb;
            return !retval;
        }
    }

    public unsafe void DebuggerExecuteCommand(string str)
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        fixed (byte* utf8 = Encoding.UTF8.GetBytes(str))
        {
            Native.GB_debugger_execute_command(_gb, utf8);
        }
    }

    public void DebuggerLoadSymbolFile(string path)
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Couldn't load symbol file '{path}'.");
        Native.GB_debugger_load_symbol_file(_gb, path);
    }

    // ================================================================================
    // Other
    // ================================================================================

    public void Dispose()
    {
        if (_gb == IntPtr.Zero)
            throw AlreadyDisposed();

        if (_gb != IntPtr.Zero)
        {
            Native.GB_free(_gb);
            Native.GB_dealloc(_gb);
            _gb = IntPtr.Zero;
        }
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    Exception AlreadyDisposed()
    {
        return new Exception("SameBoySharp: Tried to use a SameBoy after disposing it");
    }
}
