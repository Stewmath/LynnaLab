using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SameBoySharp;

/// <summary>
/// Represents a SameBoy instance.
///
/// In order to use this class, you must call SetPixelsOutput() or SetRenderingDisabled() before
/// calling Run() or RunFrame(), otherwise a segfault will occur.
///
/// You should also load a BIOS first, unless you want to set up the initial hardware state
/// manually.
/// </summary>
public unsafe partial class SameBoy : IDisposable
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


    IntPtr _gb;
    Native.GB_log_callback_t? _logCallback;
    Native.GB_vblank_callback_t? _vblankCallback;

    Native.GB_registers_t* _native_registers = null;
    RegisterList? _managed_registers = null;

    bool renderingDisabled = false;
    bool pixelOutputAssigned = false;

    // ================================================================================
    // Register access
    // ================================================================================

    public RegisterList Registers
    {
        get
        {
            if (_managed_registers == null)
            {
                _native_registers = Native.GB_get_registers(_gb);
                _managed_registers = new RegisterList(_native_registers);
            }
            return _managed_registers;
        }
    }

    // ================================================================================
    // Base
    // ================================================================================

    public uint Run()
    {
        VerifyNotDisposed();

#if DEBUG
        if (!(renderingDisabled || pixelOutputAssigned))
            throw new Exception("SameBoy: Cannot call Run() before setting render output");
#endif

        return Native.GB_run(_gb);
    }

    public ulong RunFrame()
    {
        VerifyNotDisposed();

#if DEBUG
        if (!(renderingDisabled || pixelOutputAssigned))
            throw new Exception("SameBoy: Cannot call RunFrame() before setting render output");
#endif

        return Native.GB_run_frame(_gb);
    }

    public void LoadBootRom(string path)
    {
        VerifyNotDisposed();

        int error = Native.GB_load_boot_rom(_gb, path);
        if (error != 0)
            throw new Exception($"Error {error} loading boot ROM: " + path);
    }

    public void LoadRom(string path)
    {
        VerifyNotDisposed();

        if (Native.GB_load_rom(_gb, path) != 0)
            throw new InvalidOperationException("Sameboy failed to load ROM: " + path);
    }

    public bool IsCgb()
    {
        VerifyNotDisposed();

        return Native.GB_is_cgb(_gb);
    }

    public void SetRenderingDisabled(bool disabled)
    {
        VerifyNotDisposed();

        Native.GB_set_rendering_disabled(_gb, disabled ? (byte)1 : (byte)0);
        renderingDisabled = disabled;
    }

    public void SetTurboMode(bool on, bool noFrameSkip = false)
    {
        VerifyNotDisposed();

        Native.GB_set_turbo_mode(_gb, on ? (byte)1 : (byte)0, noFrameSkip ? (byte)1 : (byte)0);
    }

    // ================================================================================
    // Video
    // ================================================================================

    public IntPtr GetFrameBuffer()
    {
        VerifyNotDisposed();

        return Native.GB_get_pixels_output(_gb);
    }

    public void SetVBlankCallback(Action<VBlankType> action)
    {
        VerifyNotDisposed();

        _vblankCallback = (gb, type) => action(type);

        Native.GB_set_vblank_callback(_gb, _vblankCallback);
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
        VerifyNotDisposed();

        ushort r, rb;
        fixed (byte* utf8 = Encoding.UTF8.GetBytes(str))
        {
            bool retval = Native.GB_debugger_evaluate(_gb, utf8, &r, &rb);
            result = r;
            result_bank = (short)rb;
            return !retval;
        }
    }

    public unsafe bool DebuggerEvaluate(string str, out ushort result)
    {
        return DebuggerEvaluate(str, out result, out short _);
    }

    public unsafe void DebuggerExecuteCommand(string str)
    {
        VerifyNotDisposed();

        fixed (byte* utf8 = Encoding.UTF8.GetBytes(str))
        {
            Native.GB_debugger_execute_command(_gb, utf8);
        }
    }

    public void DebuggerLoadSymbolFile(string path)
    {
        VerifyNotDisposed();

        if (!File.Exists(path))
            throw new FileNotFoundException($"Couldn't load symbol file '{path}'.");
        Native.GB_debugger_load_symbol_file(_gb, path);
    }

    // ================================================================================
    // Console I/O
    // ================================================================================

    public void SetLogCallback(Action<string> callback)
    {
        VerifyNotDisposed();

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
        VerifyNotDisposed();

        return Native.GB_get_rom_crc32(_gb);
    }

    public string GetRomTitle()
    {
        VerifyNotDisposed();

        var sb = new StringBuilder(17);
        Native.GB_get_rom_title(_gb, sb);
        return sb.ToString();
    }

    public byte SafeReadMemory(ushort addr)
    {
        VerifyNotDisposed();

        return Native.GB_safe_read_memory(_gb, addr);
    }

    // ================================================================================
    // Other
    // ================================================================================

    public void Dispose()
    {
        VerifyNotDisposed();

        Native.GB_free(_gb);
        Native.GB_dealloc(_gb);
        _gb = IntPtr.Zero;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    void VerifyNotDisposed()
    {
#if DEBUG
        if (_gb == IntPtr.Zero)
            throw new Exception("SameBoySharp: Tried to use a SameBoy instance after disposing it");
#endif
    }
}
