namespace Emulation;

public class GameboySnapshot : GameboyStateBase
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public GameboySnapshot(OraclesEmulator emulator) : base(emulator)
    {
        UpdateState();
    }

    // ================================================================================
    // Variables
    // ================================================================================

    short romBank, sramBank, vramBank, wramBank;

    byte[] wram;
    byte[] hram;
    byte[] vram;
    byte[] sram;
    byte[] ioram;
    byte[] oam;
    byte ie;

    Queue<Action> vblankWriteActions = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public override short RomBank { get { return romBank; } }
    public override short SramBank { get { return sramBank; } }
    public override short VramBank { get { return vramBank; } }
    public override short WramBank { get { return wramBank; } }

    public override ReadOnlySpan<byte> Rom { get { return Emulator.Rom; } }
    public override ReadOnlySpan<byte> Wram { get { return wram; } }
    public override ReadOnlySpan<byte> Hram { get { return hram; } }
    public override ReadOnlySpan<byte> Vram { get { return vram; } }
    public override ReadOnlySpan<byte> Sram { get { return sram; } }
    public override ReadOnlySpan<byte> IORam { get { return ioram; } }
    public override ReadOnlySpan<byte> OAM { get { return oam; } }
    public override byte IE { get { return ie; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    public override void WriteByte(GbPointer ptr, byte value)
    {
        DoDelayed(() =>
        {
            base.WriteByteHelper(ptr, value);
        });
    }

    public void WriteWram(int addr, byte value)
    {
        DoDelayed(() =>
        {
            Emulator.Wram[addr] = value;
        });
    }

    public override void OrByte(GbPointer ptr, byte mask)
    {
        DoDelayed(() =>
        {
            base.WriteByteHelper(ptr, (byte)(ReadByte(ptr) | mask));
        });
    }

    public override void AndByte(GbPointer ptr, byte mask)
    {
        DoDelayed(() =>
        {
            base.WriteByteHelper(ptr, (byte)(ReadByte(ptr) & mask));
        });
    }

    /// <summary>
    /// Writes don't do anything until this is called, in order to ensure writes are atomic and do
    /// not accidentally happen at different times while the gameboy thread is running. This is
    /// called once per imgui render pass.
    ///
    /// (This technically shouldn't be necessary since we lock the emulator thread while rendering
    /// the imgui interface, but it does no harm.)
    /// </summary>
    public void FlushWrites()
    {
        var actions = vblankWriteActions.ToList();
        vblankWriteActions.Clear();
        Emulator.DoAtVBlank(() => { foreach (var a in actions) a(); });
    }

    // ================================================================================
    // Internal methods
    // ================================================================================

    /// <summary>
    /// This is called from the gameboy execution thread. For thread safety, lock that thread while
    /// using the snapshot for other things.
    /// </summary>
    [MemberNotNull(nameof(wram), nameof(hram), nameof(vram), nameof(sram), nameof(ioram), nameof(oam))]
    internal void UpdateState()
    {
        romBank = Emulator.RomBank;
        sramBank = Emulator.SramBank;
        vramBank = Emulator.VramBank;
        wramBank = Emulator.WramBank;

        wram = Emulator.Wram.ToArray();
        hram = Emulator.Hram.ToArray();
        vram = Emulator.Vram.ToArray();
        sram = Emulator.Sram.ToArray();
        ioram = Emulator.IORam.ToArray();
        oam = Emulator.OAM.ToArray();
        ie = Emulator.IE;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// If the emulator is not paused, don't write the memory until the vblank, on the gameboy
    /// emulation thread.
    ///
    /// If the emulator is paused, then write the memory immediately.
    ///
    /// When the emulator is in the paused state, it tends to be in a safe spot of execution
    /// (immediately before the vblank handler), though this is slightly different than DoAtVBlank
    /// (at a specific opcode in the vblank handler). Either way it should be fairly safe for memory
    /// writes, unless the game itself is lagging.
    ///
    /// Race conditions are still technically possible (execution resumes between reading
    /// DebuggerIsStopped and writing the memory); but all of this is just a best-effort attempt to
    /// apply memory writes at a stable time in the gameboy's emulation.
    /// </summary>
    void DoDelayed(Action action)
    {
        if (Emulator.DebuggerIsStopped)
        {
            action();
            UpdateState(); // Keep this up-to-date so hex editor sees it
        }
        else
            vblankWriteActions.Enqueue(action);
    }
}
