// Enums whose values should match up with corresponding enums in libsameboy.

using System.Runtime.InteropServices;

namespace SameBoySharp;

public partial class SameBoy
{
    public enum Model : int
    {
        GB_MODEL_FAMILY_MASK = 0xF00,
        GB_MODEL_DMG_FAMILY = 0x000,
        GB_MODEL_MGB_FAMILY = 0x100,
        GB_MODEL_CGB_FAMILY = 0x200,

        GB_MODEL_GBP_BIT = 0x20,
        GB_MODEL_PAL_BIT = 0x40,
        GB_MODEL_NO_SFC_BIT = 0x80,

        /* ===============================
        * DMG / SGB family (0x000)
        * =============================== */

        // GB_MODEL_DMG_0 = 0x000,
        // GB_MODEL_DMG_A = 0x001,
        GB_MODEL_DMG_B = 0x002,
        // GB_MODEL_DMG_C = 0x003,

        GB_MODEL_SGB = 0x004,

        GB_MODEL_SGB_NTSC = GB_MODEL_SGB,
        GB_MODEL_SGB_PAL = GB_MODEL_SGB | GB_MODEL_PAL_BIT,

        GB_MODEL_SGB_NTSC_NO_SFC = GB_MODEL_SGB | GB_MODEL_NO_SFC_BIT,
        GB_MODEL_SGB_NO_SFC = GB_MODEL_SGB_NTSC_NO_SFC,
        GB_MODEL_SGB_PAL_NO_SFC = GB_MODEL_SGB | GB_MODEL_NO_SFC_BIT | GB_MODEL_PAL_BIT,

        /* ===============================
        * MGB family (0x100)
        * =============================== */

        GB_MODEL_MGB = 0x100,

        GB_MODEL_SGB2 = 0x101,
        GB_MODEL_SGB2_NO_SFC = GB_MODEL_SGB2 | GB_MODEL_NO_SFC_BIT,

        /* ===============================
        * CGB / AGB family (0x200)
        * =============================== */

        GB_MODEL_CGB_0 = 0x200,
        GB_MODEL_CGB_A = 0x201,
        GB_MODEL_CGB_B = 0x202,
        GB_MODEL_CGB_C = 0x203,
        GB_MODEL_CGB_D = 0x204,
        GB_MODEL_CGB_E = 0x205,

        // GB_MODEL_AGB_0 = 0x206,
        GB_MODEL_AGB_A = 0x207,

        /* Game Boy Player variants */
        GB_MODEL_GBP_A = GB_MODEL_AGB_A | GB_MODEL_GBP_BIT,

        /* Aliases */
        GB_MODEL_AGB = GB_MODEL_AGB_A,
        GB_MODEL_GBP = GB_MODEL_GBP_A,

        // GB_MODEL_AGB_B = 0x208,
        // GB_MODEL_AGB_E = 0x209,
        // GB_MODEL_GBP_E = GB_MODEL_AGB_E | GB_MODEL_GBP_BIT,
    }

    public enum BorderMode : int
    {
        GB_BORDER_SGB,
        GB_BORDER_NEVER,
        GB_BORDER_ALWAYS,
    }

    public enum LogAttributes : byte
    {
        GB_LOG_BOLD = 1,
        GB_LOG_DASHED_UNDERLINE = 2,
        GB_LOG_UNDERLINE = 4,
    }

    public enum VBlankType : int
    {
        GB_VBLANK_TYPE_NORMAL_FRAME,
        GB_VBLANK_TYPE_LCD_OFF,
        GB_VBLANK_TYPE_ARTIFICIAL,
        GB_VBLANK_TYPE_REPEAT
    }

    public enum GBKey : int
    {
        GB_KEY_RIGHT,
        GB_KEY_LEFT,
        GB_KEY_UP,
        GB_KEY_DOWN,
        GB_KEY_A,
        GB_KEY_B,
        GB_KEY_SELECT,
        GB_KEY_START,
        GB_KEY_MAX
    }

    public enum GBKeyMask : int
    {
        GB_KEY_RIGHT_MASK =  1 << GBKey.GB_KEY_RIGHT,
        GB_KEY_LEFT_MASK =   1 << GBKey.GB_KEY_LEFT,
        GB_KEY_UP_MASK =     1 << GBKey.GB_KEY_UP,
        GB_KEY_DOWN_MASK =   1 << GBKey.GB_KEY_DOWN,
        GB_KEY_A_MASK =      1 << GBKey.GB_KEY_A,
        GB_KEY_B_MASK =      1 << GBKey.GB_KEY_B,
        GB_KEY_SELECT_MASK = 1 << GBKey.GB_KEY_SELECT,
        GB_KEY_START_MASK =  1 << GBKey.GB_KEY_START,
    }

    public enum GBDirectAccess : int
    {
        ROM,
        RAM,
        CART_RAM,
        VRAM,
        HRAM,
        IO,
        BOOTROM,
        OAM,
        BGP,
        OBP,
        IE,
        ROM0,
    }

    // ================================================================================
    // Structs
    // ================================================================================

    public struct GB_sample_t
    {
        public short left, right;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct GB_registers_t
    {
        [FieldOffset(0)] public ushort af;
        [FieldOffset(2)] public ushort bc;
        [FieldOffset(4)] public ushort de;
        [FieldOffset(6)] public ushort hl;
        [FieldOffset(8)] public ushort sp;
        [FieldOffset(10)] public ushort pc;

        [FieldOffset(0)] public byte f;
        [FieldOffset(1)] public byte a;
        [FieldOffset(2)] public byte c;
        [FieldOffset(3)] public byte b;
        [FieldOffset(4)] public byte e;
        [FieldOffset(5)] public byte d;
        [FieldOffset(6)] public byte l;
        [FieldOffset(7)] public byte h;
    }

    /// <summary>
    /// A wrapper around register accesses. As a ref struct, this never leaves the stack, so using
    /// this is just as efficient as passing a pointer around, without needing any unsafe pointer
    /// usage. Memory safety is guaranteed in the debug configuration.
    /// </summary>
    public readonly unsafe ref struct RegisterRef
    {
        internal RegisterRef(SameBoy gb, SameBoy.GB_registers_t* registers)
        {
#if DEBUG
            this.gb = gb;
#endif
            this.registers = ref *registers;
        }

#if DEBUG
        readonly SameBoy gb;
#endif

        readonly ref SameBoy.GB_registers_t registers;

        public ref ushort AF
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.af;
            }
        }
        public ref ushort BC
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.bc;
            }
        }
        public ref ushort DE
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.de;
            }
        }
        public ref ushort HL
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.hl;
            }
        }
        public ref ushort SP
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.sp;
            }
        }
        public ref ushort PC
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.pc;
            }
        }

        public ref byte A
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.a;
            }
        }
        public ref byte F
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.f;
            }
        }
        public ref byte B
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.b;
            }
        }
        public ref byte C
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.c;
            }
        }
        public ref byte D
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.d;
            }
        }
        public ref byte E
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.e;
            }
        }
        public ref byte H
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.h;
            }
        }
        public ref byte L
        {
            get
            {
                VerifyNotDisposed();
                return ref registers.l;
            }
        }


        void VerifyNotDisposed()
        {
#if DEBUG
            gb.VerifyNotDisposed();
#endif
        }
    }
}
