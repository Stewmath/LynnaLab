#nullable enable
namespace LynnaLib;

using System.IO;
using System.Runtime.InteropServices;
using SameBoySharp;

public class OraclesEmulator : IDisposable
{
    private static readonly log4net.ILog log = LogHelper.GetLogger();

    // ================================================================================
    // Constructors
    // ================================================================================

    public OraclesEmulator(string romPath)
    {
        this.romPath = romPath;

        gb = new SameBoy(SameBoy.Model.GB_MODEL_CGB_E);
        gb.LoadBootRomFromBuffer(Helper.ReadResourceFileBytes("LynnaLib.resource.cgb_boot_fast.bin"));
        gb.LoadRom(romPath);
        gb.DebuggerLoadSymbolFile(Path.ChangeExtension(romPath, ".sym"));

        gb.SetRgbEncodeCallback(Color.RgbToUInt);

        pixelData = Marshal.AllocHGlobal(160 * 144 * 4);

        // Fill the pixel data with black to start
        for (int i = 0; i < 160 * 144 * 4; i++)
        {
            Marshal.WriteByte(pixelData + i, 0);
        }

        gb.SetPixelsOutput(pixelData);

        Initialize();

        // Try to load savefile; may fail.
        gb.LoadBattery(Path.ChangeExtension(romPath, ".sav"));

        // Register hooks on which to write the save file to disk.
        // Note, there are two functions each with these names... but it doesn't really matter which
        // one it gets resolved to - one calls the other, ultimately.
        // TODO: Also save the SRAM when exiting LynnaLab.
        foreach (var hook in new string[] {"initializeFile", "eraseFile", "saveFile"})
        {
            if (TryEvaluate(hook, out var funcAddr))
            {
                AddBreakpoint(funcAddr, FileManagementFunctionCallback);
            }
            else
            {
                log.Warn($"Couldn't hook {hook} function");
            }
        }
    }

    // ================================================================================
    // Variables
    // ================================================================================

    SameBoy gb;
    string romPath;
    IntPtr pixelData;

    Dictionary<GbAddr, Action> breakpoints = new();

    // ================================================================================
    // Properties
    // ================================================================================

    public SameBoy GB { get { return gb; } }

    public IntPtr PixelData { get { return pixelData; } }

    public short RomBank
    {
        get
        {
            gb.GetDirectAccess(SameBoy.GBDirectAccess.ROM, out short bank);
            return bank;
        }
    }
    public short VramBank { get { return (short)(gb.SafeReadMemory(0xff4f) & 1); } }
    public short WramBank { get { return (short)(gb.SafeReadMemory(0xff70) & 7); } }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Runs some indeterminate amount of code before returning. Similar to gb.Run but with a bunch
    /// of hacks to implement advanced functionality.
    /// </summary>
    public void Run()
    {
        gb.Run();

        GbAddr addr = ConstructAddr(gb.Registers.PC);

        if (breakpoints.TryGetValue(addr, out Action? act))
            act();
    }

    // TODO: Need to test this
    public byte ReadSymbolByte(string symbol)
    {
        if (!TryEvaluate(symbol, out var addr))
            throw new Exception($"ReadSymbolByte: Failed to resolve symbol \"{symbol}\"");

        if (!addr.IsBanked)
            return gb.SafeReadMemory(addr.BaseAddr);
        else
            throw new Exception("ReadSymbolByte: Banked reads not implemented yet");
    }

    /// <summary>
    /// Do something when execution reaches a particular line. The callback will be invoked on
    /// whatever thread called OraclesEmulator.Run().
    /// </summary>
    public void AddBreakpoint(GbAddr addr, Action act)
    {
        breakpoints.Add(addr, act);
    }

    /// <summary>
    /// Wrapper over SameBoy.DebuggerEvaluate. Returns true on success.
    /// </summary>
    public bool TryEvaluate(string str, out GbAddr addr)
    {
        if (gb.DebuggerEvaluate(str, out ushort result, out short result_bank))
        {
            addr = new(result, result_bank);
            return false;
        }

        addr = new(result, result_bank);
        return true;
    }

    public void Dispose()
    {
        gb.Dispose();
        gb = null!;
        Marshal.FreeHGlobal(pixelData);
        pixelData = IntPtr.Zero;
    }

    // ================================================================================
    // Private methods
    // ================================================================================

    /// <summary>
    /// Before exiting the constructor, we run to the end of the bootrom. Even though we're using
    /// the fast bios which boots to the game instantly, it has an annoying chime at the beginning.
    /// So let's execute this before sound sample callbacks have been established.
    /// </summary>
    void Initialize()
    {
        gb.SetTurboMode(true);

        while (gb.Registers.PC != 0x100)
            gb.Run();

        gb.WriteMemory(0xff12, 0); // Fix fast bios chime noise

        gb.SetTurboMode(false);
    }

    /// <summary>
    /// An alternative to using the fast BIOS. Less accurate, but works for the Oracles.
    /// </summary>
    void InitializeWithoutBios()
    {
        gb.WriteMemory(0xff4c, 0x00); // GBC hardware mode
        gb.WriteMemory(0xff50, 0x01); // Disable BIOS mapping

        // - LCD state?
        // - Audio setup
        // - Clear VRAM
        // - Clear WRAM
        // - Clear OAM

        gb.Registers.SP = 0xfffe;
        gb.Registers.A = 0x11; // GBC
        gb.Registers.BC = 0x0113;
        gb.Registers.DE = 0x00d8;
        gb.Registers.HL = 0x014d;
        gb.Registers.PC = 0x100; // Entry point
    }

    GbAddr ConstructAddr(ushort baseAddr)
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
                return new(baseAddr, -1); // Don't know how to get SRAM bank
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
                // Should never reach here
                return new(baseAddr, -1);
        }
    }

    /// <summary>
    /// Called when one of the file management functions is called. It's a signal to flush the save
    /// data to disk.
    /// </summary>
    void FileManagementFunctionCallback()
    {
        if (RunToCurrentFunctionEnd())
        {
            log.Info("Saving battery to disk");
            gb.SaveBattery(Path.ChangeExtension(romPath, ".sav"));
        }
        else
        {
            log.Warn("Never reached the end of the file save/erase function?");
        }
    }

    /// <summary>
    /// Run to the end of the current function. It assumes we've reached the end when the stack
    /// pointer is decremented to before the initial position. Takes a timeout value in milliseconds
    /// (in gameboy time).
    ///
    /// Returns false if it took too long.
    /// </summary>
    bool RunToCurrentFunctionEnd(int timeout = 100)
    {
        int initialSP = gb.Registers.SP;

        int cycleTimeout = 8000000 * timeout / 1000;

        while (gb.Registers.SP <= initialSP)
        {
            cycleTimeout -= (int)gb.Run();

            if (cycleTimeout < 0)
                return false;
        }

        return true;
    }

}

/// <summary>
/// Represents a gameboy address, including (optionally) a bank number.
/// </summary>
public readonly struct GbAddr
{
    public GbAddr(ushort addr, short bank)
    {
        BaseAddr = addr;
        Bank = bank;

        // TODO: Do some validation here? I'm not really sure whether "bank = -1" should ever be a
        // thing. Anyway, don't go crazy with validation since this is called by ExecutionCallback.
    }

    public ushort BaseAddr { get; init; }
    public short Bank { get; init; }

    public bool IsBanked { get { return Bank != -1; } }


    // Equal/HashCode overrides for efficient dictionary usage

    public override bool Equals(object? obj)
    {
        return obj is GbAddr o
               && BaseAddr == o.BaseAddr
               && Bank == o.Bank;
    }

    public override int GetHashCode()
    {
        return (int)(BaseAddr.GetHashCode() * 0x9e3779b9 + Bank);
    }
}
