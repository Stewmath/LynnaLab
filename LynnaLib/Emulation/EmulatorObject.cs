namespace Emulation;

public class EmulatorObject
{
    // ================================================================================
    // Constructors
    // ================================================================================
    public EmulatorObject(OraclesEmulator emulator, ushort address)
    {
        this.emulator = emulator;
        this.address = address;

        if (!(address >= 0xd000 && address < 0xe000 && (address % 0x40) == 0))
            throw new Exception($"EmulatorObject: Invalid address {address:X4}");
    }

    // ================================================================================
    // Variables
    // ================================================================================

    private readonly OraclesEmulator emulator;

    /// <summary>
    /// Start address of object memory, as a system bus address.
    /// Must be between 0xd000-0xdfff, and must be a multiple of 0x40.
    /// </summary>
    private readonly ushort address;

    // ================================================================================
    // Properties
    // ================================================================================

    public Span<byte> Memory
    {
        get { return emulator.Wram.Slice(address - 0xc000, 0x40); }
    }

    public ref byte Enabled { get { return ref Memory[0]; } }
    public ref byte Y { get { return ref Memory[0xb]; } }
    public ref byte X { get { return ref Memory[0xd]; } }
    public ref byte Z { get { return ref Memory[0xf]; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    // ================================================================================
    // Private methods
    // ================================================================================
}
