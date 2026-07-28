namespace Emulation;

public class GameboySnapshot : GameboyStateBase
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public GameboySnapshot(OraclesEmulator emulator) : base(emulator)
    {
        RomBank = Emulator.RomBank;
        SramBank = Emulator.SramBank;
        VramBank = Emulator.VramBank;
        WramBank = Emulator.WramBank;

        wram = Emulator.Wram.ToArray();
        hram = Emulator.Hram.ToArray();
        vram = Emulator.Vram.ToArray();
        sram = Emulator.Sram.ToArray();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    readonly byte[] wram;
    readonly byte[] hram;
    readonly byte[] vram;
    readonly byte[] sram;

    // ================================================================================
    // Properties
    // ================================================================================

    public override short RomBank { get; }
    public override short SramBank { get; }
    public override short VramBank { get; }
    public override short WramBank { get; }

    public override Span<byte> Rom { get { return Emulator.Rom; } }
    public override Span<byte> Wram { get { return wram; } }
    public override Span<byte> Hram { get { return hram; } }
    public override Span<byte> Vram { get { return vram; } }
    public override Span<byte> Sram { get { return sram; } }
}
