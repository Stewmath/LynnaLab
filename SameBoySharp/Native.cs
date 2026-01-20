using System;
using System.Runtime.InteropServices;
using System.Text;

// Native interop for libsameboy (v1.0.2).

namespace SameBoySharp;

internal unsafe static class Native
{
    private const string DLL = "sameboy"; // sameboy.dll / libsameboy.so

    // ================================================================================
    // Structs
    // ================================================================================

    internal struct GB_registers_t
    {
        internal ushort af, bc, de, hl, sp, pc;
    }

    // ================================================================================
    // Callbacks
    // ================================================================================

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void GB_vblank_callback_t(
        IntPtr gb,
        SameBoy.VBlankType type);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void GB_log_callback_t(
        IntPtr gb,
        IntPtr message,
        SameBoy.LogAttributes attributes);

    // ================================================================================
    // Base
    // ================================================================================

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr GB_alloc();

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_dealloc(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr GB_init(IntPtr gb, SameBoy.Model model);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_free(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool GB_is_inited(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GB_run(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern ulong GB_run_frame(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_reset(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GB_load_boot_rom(IntPtr gb, string path);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal static extern int GB_load_rom(IntPtr gb, string path);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_load_rom_from_buffer(IntPtr gb, byte[] buffer, UIntPtr size);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern bool GB_is_cgb(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_set_rendering_disabled(IntPtr gb, byte disabled);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_set_turbo_mode(IntPtr gb, byte on, byte no_frame_skip);

    // ================================================================================
    // Video
    // ================================================================================

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr GB_get_pixels_output(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_set_vblank_callback(IntPtr gb, GB_vblank_callback_t callback);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_set_border_mode(
        IntPtr gb,
        SameBoy.BorderMode mode);

    // ================================================================================
    // Debugger
    // ================================================================================

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    internal unsafe static extern bool GB_debugger_evaluate(
        IntPtr gb, byte* str, ushort* result, ushort* result_bank);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal unsafe static extern void GB_debugger_execute_command(
        IntPtr gb, byte* str);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    internal unsafe static extern void GB_debugger_load_symbol_file(IntPtr gb, string path);

    // ================================================================================
    // Console I/O
    // ================================================================================

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_set_log_callback(
        IntPtr gb,
        GB_log_callback_t callback);

    // ================================================================================
    // Inspection
    // ================================================================================

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern GB_registers_t* GB_get_registers(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GB_get_rom_crc32(IntPtr gb);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GB_get_rom_title(IntPtr gb, StringBuilder title);

    [DllImport(DLL, CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte GB_safe_read_memory(IntPtr gb, ushort addr);
}
