using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SameBoySharp;

/// <summary>
/// Represents a SameBoy instance.
///
/// libsameboy has some particularities where using it the wrong way can cause segfaults. In a
/// nutshell:
///
/// - You must call SetPixelsOutput() and SetRgbEncodeCallback() (or just SetRenderingDisabled())
///   before calling Run() or RunFrame().
/// - If you're using audio, you must call ApuSetSampleCallback() before SetSampleRate().
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
    Native.GB_input_callback_t? _inputCallback;
    Native.GB_input_callback_t? _asyncInputCallback;
    Native.GB_vblank_callback_t? _vblankCallback;
    Native.GB_rgb_encode_callback_t? _rgbEncodeCallback;
    Native.GB_sample_callback_t? _sampleCallback;
    Native.GB_execution_callback_t? _executionCallback;

    GB_registers_t* _native_registers = null;

    bool renderingDisabled = false;
    bool pixelOutputAssigned = false;
    bool disposed = false;

    // ================================================================================
    // Register access
    // ================================================================================

    public RegisterRef Registers
    {
        get
        {
            if (_native_registers == null)
            {
                _native_registers = Native.GB_get_registers(_gb);
            }
            return new RegisterRef(this, _native_registers);
        }
    }

    // ================================================================================
    // Base
    // ================================================================================

    public uint Run()
    {
        VerifyNotDisposed();
        VerifyRenderTargetValid();

        return Native.GB_run(_gb);
    }

    public ulong RunFrame()
    {
        VerifyNotDisposed();
        VerifyRenderTargetValid();

        return Native.GB_run_frame(_gb);
    }

    public void LoadBootRom(string path)
    {
        VerifyNotDisposed();

        int error = Native.GB_load_boot_rom(_gb, path);
        if (error != 0)
            throw new Exception($"Error {error} loading boot ROM: " + path);
    }

    public unsafe void LoadBootRomFromBuffer(Span<byte> buffer)
    {
        VerifyNotDisposed();

        fixed (byte* ptr = buffer)
        {
            Native.GB_load_boot_rom_from_buffer(_gb, (IntPtr)ptr, (nuint)buffer.Length);
        }
    }

    public void LoadRom(string path)
    {
        VerifyNotDisposed();

        if (Native.GB_load_rom(_gb, path) != 0)
            throw new InvalidOperationException("Sameboy failed to load ROM: " + path);
    }

    public unsafe Span<byte> GetDirectAccess(GBDirectAccess access, out short bank)
    {
        VerifyNotDisposed();

        nuint size;
        short b;

        void* data = (void*)Native.GB_get_direct_access(_gb, access, &size, &b);

        bank = b;
        return new Span<byte>(data, (int)size);
    }

    public Span<byte> GetDirectAccess(GBDirectAccess access)
    {
        return GetDirectAccess(access, out _);
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

    /// <summary>
    /// You must pass in a pointer to unmanaged memory, ie. allocated with Marshal.AllocHGlobal,
    /// which is at least 160x144x4 bytes large (more if there is a SGB border).
    /// </summary>
    public unsafe void SetPixelsOutput(IntPtr output)
    {
        VerifyNotDisposed();

        Native.GB_set_pixels_output(_gb, output);

        pixelOutputAssigned = output != IntPtr.Zero;
    }

    public void SetRgbEncodeCallback(Func<byte, byte, byte, uint> callback)
    {
        VerifyNotDisposed();

        _rgbEncodeCallback = (gb, r, g, b) => callback(r, g, b);

        Native.GB_set_rgb_encode_callback(_gb, _rgbEncodeCallback);
    }

    public void SetVBlankCallback(Action<VBlankType>? action)
    {
        VerifyNotDisposed();

        if (action != null)
            _vblankCallback = (gb, type) => action(type);
        else
            _vblankCallback = null;

        Native.GB_set_vblank_callback(_gb, _vblankCallback);
    }

    // ================================================================================
    // Audio
    // ================================================================================

    public uint GetSampleRate()
    {
        VerifyNotDisposed();

        return Native.GB_get_sample_rate(_gb);
    }

    public void SetSampleRate(uint rate)
    {
        VerifyNotDisposed();

#if DEBUG
        if (_sampleCallback == null && rate != 0)
            throw new Exception("SameBoy: Must call ApuSetSampleCallback() before SetSampleRate().");
#endif

        Native.GB_set_sample_rate(_gb, rate);
    }

    /// <summary>
    /// Callback takes two parameters: left sample, and right sample.
    /// </summary>
    public void ApuSetSampleCallback(Action<GB_sample_t>? action)
    {
        VerifyNotDisposed();

        if (action != null)
        {
            _sampleCallback = (gb, sample) => action(*sample);
        }
        else
        {
            _sampleCallback = null;
        }

        Native.GB_apu_set_sample_callback(_gb, _sampleCallback);
    }

    // ================================================================================
    // Player controls
    // ================================================================================

    public void SetKeyMask(GBKeyMask mask)
    {
        VerifyNotDisposed();

        Native.GB_set_key_mask(_gb, mask);
    }

    // ================================================================================
    // Save data
    // ================================================================================

    public int SaveBattery(string path)
    {
        VerifyNotDisposed();

        return Native.GB_save_battery(_gb, path);
    }

    public int LoadBattery(string path)
    {
        VerifyNotDisposed();

        return Native.GB_load_battery(_gb, path);
    }

    // ================================================================================
    // Debugger functions
    // ================================================================================

    /// <summary>
    /// Returns FALSE on success.
    ///
    /// result_bank = -1 if there is no bank number.
    /// </summary>
    public unsafe bool DebuggerEvaluate(string str, out ushort result, out short result_bank)
    {
        VerifyNotDisposed();

        ushort r, rb;
        fixed (byte* utf8 = Encoding.UTF8.GetBytes(str + '\0'))
        {
            bool retval = Native.GB_debugger_evaluate(_gb, utf8, &r, &rb);
            result = r;
            result_bank = (short)rb;
            return retval;
        }
    }

    public unsafe bool DebuggerEvaluate(string str, out ushort result)
    {
        return DebuggerEvaluate(str, out result, out short _);
    }

    public unsafe void DebuggerExecuteCommand(string str)
    {
        VerifyNotDisposed();

        fixed (byte* utf8 = Encoding.UTF8.GetBytes(str + '\0'))
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

    public void SetInputCallback(Func<string?> callback)
    {
        VerifyNotDisposed();

        _inputCallback = (gb) =>
        {
            string? s = callback();
            if (s == null)
                return 0;
            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(s + '\0');
            IntPtr ptr = Marshal.AllocHGlobal(utf8Bytes.Length);
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);
            return ptr;
        };
        Native.GB_set_input_callback(_gb, _inputCallback);
    }

    public void SetAsyncInputCallback(Func<string?> callback)
    {
        VerifyNotDisposed();

        _asyncInputCallback = (gb) =>
        {
            string? s = callback();
            if (s == null)
                return 0;
            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(s + '\0');
            IntPtr ptr = Marshal.AllocHGlobal(utf8Bytes.Length);
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);
            return ptr;
        };
        Native.GB_set_async_input_callback(_gb, _asyncInputCallback);
    }

    // ================================================================================
    // Inspection
    // ================================================================================

    /// <summary>
    /// It's recommended that you use the Registers property instead of this.
    /// </summary>
    public GB_registers_t* GetRegisters()
    {
        VerifyNotDisposed();

        return Native.GB_get_registers(_gb);
    }

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

    public void SetExecutionCallback(Action<ushort, byte> action)
    {
        VerifyNotDisposed();

        if (action == null)
            _executionCallback = null;
        else
            _executionCallback = (gb, addr, opcode) => action(addr, opcode);

        Native.GB_set_execution_callback(_gb, _executionCallback);
    }

    public void WriteMemory(ushort addr, byte value)
    {
        VerifyNotDisposed();

        Native.GB_write_memory(_gb, addr, value);
    }

    // ================================================================================
    // Other
    // ================================================================================

    public void Dispose()
    {
        VerifyNotDisposed();

        disposed = true;
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
        if (disposed)
            throw new Exception("SameBoySharp: Tried to use a SameBoy instance after disposing it");
#endif
    }

    void VerifyRenderTargetValid()
    {
#if DEBUG
        if (!(renderingDisabled || (pixelOutputAssigned && _rgbEncodeCallback != null)))
            throw new Exception("SameBoy: Cannot call Run() before setting render output");
#endif
    }
}
