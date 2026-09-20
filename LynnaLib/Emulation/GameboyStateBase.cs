namespace Emulation;

using System.Text;
using LynnaLib;

public abstract class GameboyStateBase
{
    // ================================================================================
    // Constructors
    // ================================================================================

    public GameboyStateBase(OraclesEmulator emulator)
    {
        this.Emulator = emulator;
    }

    // ================================================================================
    // Properties
    // ================================================================================

    protected OraclesEmulator Emulator { get; }

    public abstract short RomBank { get; }
    public abstract short SramBank { get; }
    public abstract short VramBank { get; }
    public abstract short WramBank { get; }

    // All RAM on the system bus is exposed through these spans (+ IE)
    public abstract ReadOnlySpan<byte> Rom { get; }
    public abstract ReadOnlySpan<byte> Wram { get; }
    public abstract ReadOnlySpan<byte> Hram { get; }
    public abstract ReadOnlySpan<byte> Vram { get; }
    public abstract ReadOnlySpan<byte> Sram { get; }
    public abstract ReadOnlySpan<byte> IORam { get; }
    public abstract ReadOnlySpan<byte> OAM { get; }
    public abstract byte IE { get; }


    public short CameraY
    {
        // TODO: Use proper labels
        get { return (short)ReadWord(
            ConstructPointer(DetermineGame() == Game.Ages ? (ushort)0xffaa : (ushort)0xffa8)); }
    }
    public short CameraX
    {
        // TODO: Use proper labels
        get { return (short)ReadWord(
            ConstructPointer(DetermineGame() == Game.Ages ? (ushort)0xffac : (ushort)0xffaa)); }
    }

    public int RoomIndex
    {
        get
        {
            return ReadSymbolByte("wActiveRoom") | (ReadSymbolByte("wActiveGroup") << 8);
        }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public GbPointer ConstructPointer(ushort baseAddr)
    {
        switch (GbPointer.GetMemoryArea(baseAddr))
        {
            case EmulatorMemoryArea.ROM0:
                return new(baseAddr, 0);
            case EmulatorMemoryArea.ROMX:
                return new(baseAddr, RomBank);
            case EmulatorMemoryArea.VRAM:
                return new(baseAddr, VramBank);
            case EmulatorMemoryArea.SRAM:
                return new(baseAddr, SramBank);
            case EmulatorMemoryArea.WRAM0:
                return new(baseAddr, 0);
            case EmulatorMemoryArea.WRAMX:
                return new(baseAddr, WramBank);
            default:
                return new(baseAddr, -1);
        }
    }

    public byte ReadByte(GbPointer pointer)
    {
        switch (pointer.GetMemoryArea())
        {
            case EmulatorMemoryArea.ROM0:
                return Rom[pointer.Addr];
            case EmulatorMemoryArea.ROMX:
                if (!pointer.IsBanked)
                    throw new ArgumentException("ReadByte: Tried to read ROM addr {pointer} with no bank");
                return Rom[pointer.Bank * 0x4000 + (pointer.Addr & 0x3fff)];
            case EmulatorMemoryArea.VRAM:
                return Vram[pointer.Addr - 0x8000];
            case EmulatorMemoryArea.SRAM:
                return Sram[pointer.Addr - 0xa000];
            case EmulatorMemoryArea.WRAM0:
                return Wram[pointer.Addr & 0xfff];
            case EmulatorMemoryArea.WRAMX:
                if (!pointer.IsBanked)
                    throw new ArgumentException("ReadByte: Tried to read WRAM addr {pointer} with no bank");
                return Wram[pointer.Bank * 0x1000 + (pointer.Addr & 0xfff)];
            case EmulatorMemoryArea.OAM:
                return OAM[pointer.Addr - 0xfe00];
            case EmulatorMemoryArea.INVALID:
                return 0xff;
            case EmulatorMemoryArea.IO:
                return IORam[pointer.Addr - 0xff00];
            case EmulatorMemoryArea.HRAM:
                return Hram[pointer.Addr - 0xff80];
            case EmulatorMemoryArea.IE:
                return IE;
            default:
                // This should never happen
                throw new EmulatorException($"ReadByte: Unimplemented read addr: {pointer}");
        }
    }

    public ushort ReadWord(GbPointer pointer)
    {
        return (ushort)(ReadByte(pointer) | (ReadByte(pointer + 1) << 8));
    }

    public byte ReadSymbolByte(string symbol)
    {
        if (!Emulator.TryEvaluate(symbol, out var ptr))
            throw new Exception($"ReadSymbolByte: Failed to resolve symbol \"{symbol}\"");

        if (!ptr.IsBanked)
            return Emulator.GB.SafeReadMemory(ptr.Addr);
        else
            return ReadByte(ptr);
    }

    public abstract void WriteByte(GbPointer ptr, byte value);
    public abstract void OrByte(GbPointer ptr, byte mask);
    public abstract void AndByte(GbPointer ptr, byte mask);

    public void WriteWord(GbPointer ptr, ushort value)
    {
        WriteByte(ptr,     (byte)(value & 0xff));
        WriteByte(ptr + 1, (byte)(value >> 8));
    }

    public bool AddressEquals(ushort addr, GbPointer ptr)
    {
        return ptr.Equals(ConstructPointer(addr));
    }

    // ================================================================================
    // Oracles-specific data handling
    // ================================================================================

    public IList<EmulatorObject> GetObjects()
    {
        List<EmulatorObject> list = new();
        for (int m = 0x1000; m < 0x2000; m+=0x40)
        {
            if (Wram[m] != 0)
                list.Add(new EmulatorObject(this, (ushort)(m + 0xc000)));
        }
        return list;
    }

    /// <summary>
    /// Make a best-effort attempt to determine what game is running. Ideally we would not rely on
    /// this result for anything.
    /// </summary>
    public Game DetermineGame()
    {
        var title = Encoding.ASCII.GetString(Rom.Slice(0x134, 9));

        if (title == "ZELDA DIN")
            return Game.Seasons;
        else if (title == "ZELDA NAY")
            return Game.Ages;
        else
            // They changed the title, this won't work =)
            return Game.Ages;
    }

    public bool GetTreasureFlag(int treasureID)
    {
        var ptr = Emulator.Evaluate("wObtainedTreasureFlags");
        var (offset, bitmask) = IndexToByteBitmask(treasureID);
        return (ReadByte(ptr + offset) & bitmask) != 0;
    }

    public void SetTreasureFlag(int treasureID, bool set)
    {
        var ptr = Emulator.Evaluate("wObtainedTreasureFlags");
        var (offset, bitmask) = IndexToByteBitmask(treasureID);
        if (set)
            OrByte(ptr, (byte)bitmask);
        else
            AndByte(ptr, (byte)(0xff ^ bitmask));
    }

    // ================================================================================
    // Protected methods
    // ================================================================================

    protected void WriteByteHelper(GbPointer ptr, byte value)
    {
        switch (ptr.GetMemoryArea())
        {
            case EmulatorMemoryArea.ROM0:
            case EmulatorMemoryArea.ROMX:
            case EmulatorMemoryArea.INVALID:
                break; // Ignore these

            case EmulatorMemoryArea.VRAM:
                ptr.AssertBanked();
                Emulator.Vram[ptr.Bank * 0x2000 + (ptr.Addr & 0x1fff)] = value;
                break;

            case EmulatorMemoryArea.SRAM:
                Emulator.Sram[ptr.Bank * 0x2000 + (ptr.Addr & 0x1fff)] = value;
                break;

            case EmulatorMemoryArea.WRAM0:
                Emulator.Wram[ptr.Addr & 0xfff] = value;
                break;

            case EmulatorMemoryArea.WRAMX:
                ptr.AssertBanked();
                Emulator.Wram[ptr.Bank * 0x1000 + (ptr.Addr & 0xfff)] = value;
                break;

            case EmulatorMemoryArea.OAM:
                Emulator.OAM[ptr.Addr & 0xff] = value;
                break;

            case EmulatorMemoryArea.HRAM:
                Emulator.Hram[ptr.Addr & 0x7f] = value;
                break;

            // These ones must go through Sameboy's write functions to work correctly.
            // (We don't use this for everything else because it uses the current loaded bank.)
            case EmulatorMemoryArea.IO:
            case EmulatorMemoryArea.IE:
                Emulator.GB.WriteMemory(ptr.Addr, value);
                break;

            default: // Should never reach here
                throw new NotImplementedException();
        }
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// Convert an index to an offset + bitmask pair for use with the game's various bit tables.
    /// </summary>
    static (int, int) IndexToByteBitmask(int i)
    {
        int bit = i % 8;
        int bitmask = (1 << bit);
        return (i / 8, bitmask);
    }
}
