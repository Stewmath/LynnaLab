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

    public abstract Span<byte> Rom { get; }
    public abstract Span<byte> Wram { get; }
    public abstract Span<byte> Hram { get; }
    public abstract Span<byte> Vram { get; }
    public abstract Span<byte> Sram { get; }

    public short CameraY
    {
        // TODO: Use proper labels
        get { return (short)ReadWord(
            ConstructPointer(DetermineGame() == Game.Ages ? (ushort)0xffaa : (ushort)0xffac)); }
    }
    public short CameraX
    {
        // TODO: Use proper labels
        get { return (short)ReadWord(
            ConstructPointer(DetermineGame() == Game.Ages ? (ushort)0xffac : (ushort)0xffaa)); }
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public GbPointer ConstructPointer(ushort baseAddr)
    {
        switch (baseAddr >> 12)
        {
            case 0x0:
            case 0x1:
            case 0x2:
            case 0x3:
                return new(baseAddr, 0);
            case 0x4:
            case 0x5:
            case 0x6:
            case 0x7:
                return new(baseAddr, RomBank);
            case 0x8:
            case 0x9:
                return new(baseAddr, VramBank);
            case 0xa:
            case 0xb:
                return new(baseAddr, SramBank);
            case 0xc:
            case 0xe:
                return new(baseAddr, 0);
            case 0xd:
                return new(baseAddr, WramBank);
            case 0xf:
                if (baseAddr < 0xfe00)
                    return new(baseAddr, WramBank);
                else
                    return new(baseAddr, -1);
            default:
                return new(baseAddr, -1);
        }
    }

    public byte ReadByte(GbPointer pointer)
    {
        if (pointer.Addr >= 0x8000 && pointer.Addr < 0xa000)
            return Vram[pointer.Addr - 0x8000];
        else if (pointer.Addr >= 0xa000 && pointer.Addr < 0xc000)
            return Sram[pointer.Addr - 0xa000];
        else if (pointer.Addr >= 0xc000 && pointer.Addr < 0xd000)
            return Wram[pointer.Addr - 0xc000];
        else if (pointer.Addr >= 0xff80)
            return Hram[pointer.Addr - 0xff80];
        else
            throw new EmulatorException($"ReadByte: Unimplemented read addr: {pointer}");
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

    public bool AddressEquals(ushort addr, GbPointer ptr)
    {
        return ptr.Equals(ConstructPointer(addr));
    }

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

    // ================================================================================
    // Private methods
    // ================================================================================

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
}
