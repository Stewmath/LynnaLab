namespace Emulation;

using LynnaLib;

/// <summary>
/// Represents an object (Link/enemy/etc) within a running emulator instance.
/// </summary>
public class EmulatorObject
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorObject(GameboyStateBase state, ushort address)
    {
        this.state = state;
        this.address = address;

        if (!(address >= 0xd000 && address < 0xe000 && (address % 0x40) == 0))
            throw new Exception($"EmulatorObject: Invalid address {address:X4}");
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private readonly GameboyStateBase state;

    /// <summary>
    /// Start address of object memory, as a system bus address.
    /// Must be between 0xd000-0xdfff, and must be a multiple of 0x40.
    /// </summary>
    private readonly ushort address;

    public ushort Address { get { return address; } }
    public GbPointer Ptr { get { return new(Address, 1); } }

    // ================================================================================
    // Properties
    // ================================================================================

    public ReadOnlySpan<byte> Memory
    {
        get { return state.Wram.Slice(address - 0xc000, 0x40); }
    }

    public byte Enabled
    {
        get => GetByte(0);
        set => WriteByte(0, value);
    }
    public byte Y
    {
        get => GetByte(0xb);
        set => WriteByte(0xb, value);
    }
    public byte X
    {
        get => GetByte(0xd);
        set => WriteByte(0xd, value);
    }
    public byte Z
    {
        get => GetByte(0xf);
        set => WriteByte(0xf, value);
    }
    public byte CollisionRadiusY
    {
        get => GetByte(0x26);
        set => WriteByte(0x26, value);
    }
    public byte CollisionRadiusX
    {
        get => GetByte(0x27);
        set => WriteByte(0x27, value);
    }

    // ================================================================================
    // Public methods
    // ================================================================================

    public FRect GetRectangle()
    {
        float w = CollisionRadiusX * 2;
        float h = CollisionRadiusY * 2;
        if (w == 0)
            w = 8;
        if (h == 0)
            h = 8;
        return new FRect(X - w / 2, Y - h / 2, w, h);
    }

    public string GetName(Project p, bool withPrefix=true)
    {
        string s = GetConstantsMapping(p).ByteToString(Memory[1]);
        if (withPrefix)
            return s;
        return GetConstantsMapping(p).RemovePrefix(s);
    }

    public ConstantsMapping GetConstantsMapping(Project p)
    {
        switch (address & 0xc0)
        {
            case 0x00:
                if (address < 0xd200)
                    return p.SpecialObjectMapping;
                else
                    return p.ItemMapping;
            case 0x40:
                return p.InteractionMapping;
            case 0x80:
                return p.EnemyMapping;
            case 0xc0:
                return p.PartMapping;
            default:
                throw new NotImplementedException();
        }
    }

    public void WriteByte(int offset, byte value)
    {
        if (offset < 0 || offset >= 0x40)
            throw new ArgumentException($"EmulatorObject.WriteByte: Offset {offset} out of range");
        state.WriteByte(Ptr + offset, value);
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    private byte GetByte(int offset)
    {
        return Memory[offset];
    }
}
