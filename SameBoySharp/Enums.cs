// Enums whose values should match up with corresponding enums in libsameboy.

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
}
