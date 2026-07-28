namespace Emulation;

public class GameboyState : GameboyStateBase
{
    public GameboyState(OraclesEmulator emulator) : base(emulator)
    {
    }

    public override short RomBank { get { return Emulator.RomBank; } }
    public override short SramBank { get { return Emulator.SramBank; } }
    public override short VramBank { get { return Emulator.VramBank; } }
    public override short WramBank { get { return Emulator.WramBank; } }

    public override Span<byte> Rom { get { return Emulator.Rom; } }
    public override Span<byte> Wram { get { return Emulator.Wram; } }
    public override Span<byte> Hram { get { return Emulator.Hram; } }
    public override Span<byte> Vram { get { return Emulator.Vram; } }
    public override Span<byte> Sram { get { return Emulator.Sram; } }
}
