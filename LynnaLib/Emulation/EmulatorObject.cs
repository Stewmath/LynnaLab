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

    // ================================================================================
    // Properties
    // ================================================================================

    // TODO: Make this read only - not safe to write while another thread is running
    public Span<byte> Memory
    {
        get { return state.Wram.Slice(address - 0xc000, 0x40); }
    }

    public ref byte Enabled { get { return ref Memory[0]; } }
    public ref byte Y { get { return ref Memory[0xb]; } }
    public ref byte X { get { return ref Memory[0xd]; } }
    public ref byte Z { get { return ref Memory[0xf]; } }
    public ref byte CollisionRadiusY { get { return ref Memory[0x26]; } }
    public ref byte CollisionRadiusX { get { return ref Memory[0x27]; } }

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

    // ================================================================================
    // Private methods
    // ================================================================================
}
