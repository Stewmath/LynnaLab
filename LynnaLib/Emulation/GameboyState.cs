namespace Emulation;

public class LiveGameboyState : GameboyStateBase
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public LiveGameboyState(OraclesEmulator emulator) : base(emulator)
    {
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void WriteByte(GbPointer ptr, byte value)
    {
        base.WriteByteHelper(ptr, value);
    }

    // ================================================================================
    // Properties
    // ================================================================================

    public override short RomBank { get { return Emulator.RomBank; } }
    public override short SramBank { get { return Emulator.SramBank; } }
    public override short VramBank { get { return Emulator.VramBank; } }
    public override short WramBank { get { return Emulator.WramBank; } }

    public override ReadOnlySpan<byte> Rom { get { return Emulator.Rom; } }
    public override ReadOnlySpan<byte> Wram { get { return Emulator.Wram; } }
    public override ReadOnlySpan<byte> Hram { get { return Emulator.Hram; } }
    public override ReadOnlySpan<byte> Vram { get { return Emulator.Vram; } }
    public override ReadOnlySpan<byte> Sram { get { return Emulator.Sram; } }
    public override ReadOnlySpan<byte> IORam { get { return Emulator.IORam; } }
    public override ReadOnlySpan<byte> OAM { get { return Emulator.OAM; } }
    public override byte IE { get { return Emulator.IE; } }
}
