using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SameBoySharp;

namespace Emulation;

using System.Collections.Concurrent;
using LynnaLib;

public class OraclesEmulator : IDisposable
{
    private static readonly log4net.ILog log = LogHelper.GetLogger();

    // ================================================================================
    // Constructors
    // ================================================================================

    public OraclesEmulator(string romPath, EmulatorStartOptions startOptions = new())
    {
        log.Info($"Loading Emulator ROM: {romPath}");
        this.romPath = romPath;
        this.startOptions = startOptions;

        state = new LiveGameboyState(this);

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

        if (startOptions.Quickstart)
            LoadIntoRoom(startOptions.RoomIndex, startOptions.Season, startOptions.X, startOptions.Y);

        snapshot = new GameboySnapshot(this);
    }

    // ================================================================================
    // Variables
    // ================================================================================

    readonly EmulatorStartOptions startOptions;
    readonly LiveGameboyState state;

    SameBoy gb;
    string romPath;
    IntPtr pixelData;
    ConcurrentQueue<Action> vblankActionQueue = new();

    Dictionary<GbPointer, Action> breakpoints = new();
    GameboySnapshot snapshot;

    // ================================================================================
    // Properties
    // ================================================================================

    public SameBoy GB { get { return gb; } }

    /// <summary>
    /// Access live state of the gameboy as it is executing (live RAM contents).
    /// </summary>
    public LiveGameboyState LiveState { get { return state; } }

    /// <summary>
    /// Snapshot of emulator state taken during vblank. Use this instead of using LiveState directly
    /// so that we're not in the middle of some logic that would interfere with whatever we're
    /// checking.
    ///
    /// Writes which go through the Snapshot apply at next vblank.
    /// </summary>
    public GameboySnapshot Snapshot { get { return snapshot; } }

    public IntPtr PixelData { get { return pixelData; } }


    // Some state is exposed directly as with LiveState.

    public short RomBank
    {
        get
        {
            gb.GetDirectAccess(SameBoy.GBDirectAccess.ROM, out short bank);
            return bank;
        }
    }
    public short SramBank
    {
        get
        {
            gb.GetDirectAccess(SameBoy.GBDirectAccess.CART_RAM, out short bank);
            return bank;
        }
    }
    public short VramBank { get { return (short)(gb.SafeReadMemory(0xff4f) & 1); } }
    public short WramBank { get { return (short)(gb.SafeReadMemory(0xff70) & 7); } }

    public Span<byte> Rom { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.ROM); } }
    public Span<byte> Wram { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.RAM); } }
    public Span<byte> Hram { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.HRAM); } }
    public Span<byte> Vram { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.VRAM); } }
    public Span<byte> Sram { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.CART_RAM); } }
    public Span<byte> IORam { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.IO); } }
    public Span<byte> OAM { get { return gb.GetDirectAccess(SameBoy.GBDirectAccess.OAM); } }
    public ref byte IE { get { return ref gb.GetDirectAccess(SameBoy.GBDirectAccess.IE)[0]; } }

    // ================================================================================
    // Public methods
    // ================================================================================

    /// <summary>
    /// Runs some indeterminate amount of code before returning (but usually about one opcode).
    /// Similar to gb.Run but with a bunch of hacks to implement advanced functionality.
    /// This typically runs on a separate thread from the main program, so use caution.
    /// </summary>
    public void Run()
    {
        gb.Run();

        if (gb.Registers.PC == 0x44) // VBlank, after pushing all registers
        {
            snapshot.UpdateState();

            while (vblankActionQueue.TryDequeue(out Action? action))
            {
                if (action != null)
                    action();
            }
        }

        GbPointer ptr = state.ConstructPointer(gb.Registers.PC);

        if (breakpoints.TryGetValue(ptr, out Action? act))
            act();
    }

    public byte ReadSymbolByte(string symbol)
    {
        if (!TryEvaluate(symbol, out var ptr))
            throw new Exception($"ReadSymbolByte: Failed to resolve symbol \"{symbol}\"");

        return state.ReadByte(ptr);
    }

    /// <summary>
    /// Do something when execution reaches a particular line. The callback will be invoked on
    /// whatever thread called OraclesEmulator.Run().
    /// </summary>
    public void AddBreakpoint(GbPointer ptr, Action act)
    {
        breakpoints.Add(ptr, act);
    }

    /// <summary>
    /// Wrapper over SameBoy.DebuggerEvaluate. Returns true on success.
    /// </summary>
    public bool TryEvaluate(string str, out GbPointer addr)
    {
        if (gb.DebuggerEvaluate(str, out ushort result, out short result_bank))
        {
            addr = new(result, result_bank);
            return false;
        }

        addr = new(result, result_bank);
        return true;
    }

    public GbPointer Evaluate(string str)
    {
        if (!TryEvaluate(str, out GbPointer addr))
            throw new EmulatorEvalException($"OraclesEmulator: Couldn't evaluate {str}.");
        return addr;
    }

    public string DescribeAddress(GbPointer ptr)
    {
        return GB.DebuggerDescribeAddress(ptr.Addr, (ushort)ptr.Bank);
    }

    /// <summary>
    /// Reset the game at the specified room and position
    /// </summary>
    public bool LoadIntoRoom(int roomIndex, Season season, byte x, byte y)
    {
        if (!startOptions.Quickstart)
            return false;

        InitializeWithoutBios();

        // TODO: Save state to recover from error here?

        gb.SetTurboMode(true);

        try
        {
            if (!RunUntil(Evaluate("_mainLoop"), 1000000))
            {
                log.Error($"LoadIntoRoom: Didn't reach _mainLoop ({Evaluate("_mainLoop")})");
                return false;
            }

            // Note: This usually resolves to the fileManagement.s loadFile, not the bank 0
            // loadFile. But either works.
            Call("loadFile");
            RunToCurrentFunctionEnd();

            // TODO: These addresses are hardcoded for compatibility with older projects because at
            // the time of coding, hack-base does not put these labels in ramsections.
            if (LiveState.DetermineGame() == Game.Ages)
            {
                WriteByte(LiveState.ConstructPointer(0xff9d), 2); // hNextLcdInterruptBehaviour
                WriteByte(LiveState.ConstructPointer(0xffb9), 1); // hIntroInputsEnabled
            }
            else
            {
                WriteByte(LiveState.ConstructPointer(0xff9b), 2); // hNextLcdInterruptBehaviour
                WriteByte(LiveState.ConstructPointer(0xffb7), 1); // hIntroInputsEnabled
            }

            WriteByte("wDeathRespawnBuffer.group", (byte)(roomIndex >> 8));
            WriteByte("wDeathRespawnBuffer.room", (byte)(roomIndex & 0xff));
            if (season != Season.None)
                WriteByte("wDeathRespawnBuffer.stateModifier", (byte)season);
            WriteByte("wDeathRespawnBuffer.y", y);
            WriteByte("wDeathRespawnBuffer.x", x);

            // Hack initial thread states to run game thread
            GbPointer wThreadStates = LiveState.ConstructPointer(0xc2e0); // TODO: wThreadStateBuffer
            WriteWord(wThreadStates + 0 + 4, Evaluate("stubThreadStart").Addr);
            WriteWord(wThreadStates + 8 + 4, Evaluate("mainThreadStart").Addr);

            return true;
        }
        catch (EmulatorEvalException e)
        {
            log.Error($"LoadIntoRoom: {e.Message}");
            return false;
        }
        finally
        {
            gb.SetTurboMode(false);
        }
    }

    public void PlaySound(int music)
    {
        DoAtVBlank(() =>
        {
            // "playSound" is ambiguous between two functions with that name. So instead of calling
            // that, just write it into the music queue.

            // TODO: Use Evaluate("hMusicQueueTail") when possible, same for wMusicQueue
            GbPointer tailPtr = new GbPointer(LiveState.DetermineGame() == Game.Ages
                ? 0xffb5 : 0xffb3, 0);
            GbPointer queue = new GbPointer(0xc0a0, 0); // wMusicQueue

            byte tail = LiveState.ReadByte(tailPtr);
            ushort addr = (ushort)((queue.Addr & 0xff00) | tail);
            GbPointer ptr = new(addr, queue.Bank);
            WriteByte(ptr, (byte)music);
            WriteByte(tailPtr, (byte)((tail + 1) & 0xaf));
        });
    }

    public void GiveTreasure(byte treasure, byte parameter)
    {
        DoAtVBlank(() =>
        {
            GB.Registers.A = treasure;
            GB.Registers.C = parameter;
            Call("giveTreasure");
        });
    }

    public void LoseTreasure(byte treasure)
    {
        DoAtVBlank(() =>
        {
            GB.Registers.A = treasure;
            Call("loseTreasure");
        });
    }

    /// <summary>
    /// Given a treasure ID, this tries to get the parameter corresponding to that treasure (ie.
    /// level) reading treasureCollectionBehaviourTable.
    /// </summary>
    public bool GetTreasureParameterPtr(int treasureID, out GbPointer ptr)
    {
        GbPointer tablePtr = Evaluate("treasureCollectionBehaviourTable");
        byte lowByte = Snapshot.ReadByte(tablePtr + treasureID * 3);
        if (lowByte == 0)
        {
            ptr = new(0, 0);
            return false;
        }
        ptr = new(0xc600 + lowByte, 0);
        return true;
    }

    public void DoAtVBlank(Action action)
    {
        vblankActionQueue.Enqueue(action);
    }

    public void WriteByteSync(GbPointer ptr, byte value)
    {
        DoAtVBlank(() => WriteByte(ptr, value));
    }

    public void WriteWordSync(GbPointer ptr, ushort value)
    {
        DoAtVBlank(() => WriteWord(ptr, value));
    }

    public GbPointer GetGroupRoomFlags(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex > 8)
            throw new ArgumentException();

        GbPointer tablePtr = Evaluate("flagLocationGroupTable");
        return new(LiveState.ReadByte(tablePtr + groupIndex) << 8, 0);
    }

    public GbPointer GetRoomFlagPtr(int roomIndex)
    {
        return GetGroupRoomFlags(roomIndex >> 8) + (roomIndex & 0xff);
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
        gb.Registers.BC = 0x0013;
        gb.Registers.DE = 0x00d8;
        gb.Registers.HL = 0x014d;
        gb.Registers.PC = 0x100; // Entry point
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

    // ================================================================================
    // Emulator manipulation functions: memory access, stepping through functions, etc.
    // ================================================================================

    /// <summary>
    /// Run to the end of the current function. It assumes we've reached the end when the stack
    /// pointer is decremented to before the initial position. Takes a timeout value in milliseconds
    /// (in gameboy time).
    ///
    /// Returns false if it took too long.
    ///
    /// Recommended to set Turbo mode before calling this.
    /// </summary>
    bool RunToCurrentFunctionEnd(int timeout = 100)
    {
        int initialSP = gb.Registers.SP;

        long cycleTimeout = 8000000L * timeout / 1000;

        while (gb.Registers.SP <= initialSP)
        {
            cycleTimeout -= (int)gb.Run();

            if (cycleTimeout < 0)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Run until the specified address is hit.
    /// </summary>
    bool RunUntil(GbPointer pointer, int timeout = 100)
    {
        long cycleTimeout = 8000000L * timeout / 1000;

        while (!LiveState.AddressEquals(gb.Registers.PC, pointer))
        {
            cycleTimeout -= (int)gb.Run();

            if (cycleTimeout < 0)
                return false;
        }

        return true;
    }

    void Call(GbPointer pointer)
    {
        if (pointer.Addr < 0x4000)
        {
        }
        else if (pointer.Addr < 0x8000)
        {
            if (RomBank != pointer.Bank)
            {
                // Use interBankCall. Not all registers will be preserved.
                Call("interBankCall");
                gb.Registers.E = (byte)pointer.Bank;
                gb.Registers.HL = pointer.Addr;
                return;
            }
            // Else fall through
        }
        else
        {
            throw new EmulatorException($"Call {pointer}: Unimplemented region");
        }

        Push(gb.Registers.PC);
        gb.Registers.PC = pointer.Addr;
    }

    void Call(string symbol)
    {
        Call(Evaluate(symbol));
    }

    /// <summary>
    /// Push a word onto the stack.
    /// </summary>
    void Push(ushort value)
    {
        gb.Registers.SP -= 2;
        gb.WriteMemory(gb.Registers.SP, (byte)(value & 0xff));
        gb.WriteMemory((ushort)(gb.Registers.SP + 1), (byte)(value >> 8));
    }

    /// <summary>
    /// Write a byte to an address. Differs from gb.WriteMemory in that it can write to arbitrary
    /// memory banks based on the pointer passed in.
    /// </summary>
    void WriteByte(GbPointer pointer, byte value)
    {
        if (pointer.Addr >= 0xc000 && pointer.Addr < 0xd000)
            Wram[pointer.Addr - 0xc000] = value;
        else if (pointer.Addr >= 0xff80)
            Hram[pointer.Addr - 0xff80] = value;
        else
            throw new EmulatorException($"WriteByte: Unimplemented write addr: {pointer}");
    }

    void WriteByte(string symbol, byte value)
    {
        WriteByte(Evaluate(symbol), value);
    }

    void WriteWord(GbPointer addr, ushort value)
    {
        WriteByte(addr, (byte)(value & 0xff));
        WriteByte(addr + 1, (byte)(value >> 8));
    }

    // ================================================================================
    // Static methods
    // ================================================================================

    /// <summary>
    /// For reads on the system bus, choose a sane default bank, rather than whatever bank is
    /// currently loaded, for the sake of having something stable to look at.
    /// </summary>
    public static short DefaultBank(int addr)
    {
        if (addr < 0x4000)
            return 0;
        if (addr < 0x8000)
            return 1;
        if (addr < 0xa000)
            return 0;
        if (addr < 0xc000)
            return 0;
        if (addr < 0xd000)
            return 0;
        if (addr < 0xe000)
            return 1;
        if (addr < 0xf000)
            return 0;
        if (addr < 0xfe00)
            return 1;
        return -1;
    }
}

/// <summary>
/// Represents a gameboy address, including (optionally) a bank number.
/// </summary>
public readonly record struct GbPointer
{
    public GbPointer(ushort addr, short bank)
    {
        Addr = addr;
        Bank = bank;

        // TODO: Do some validation here? I'm not really sure whether "bank = -1" should ever be a
        // thing. Anyway, don't go crazy with validation since this is called by ExecutionCallback.
    }

    public GbPointer(int addr, short bank) : this((ushort)addr, bank) {}

    public ushort Addr { get; init; }
    public short Bank { get; init; }

    public bool IsBanked { get { return Bank != -1; } }

    public EmulatorMemoryArea GetMemoryArea()
    {
        return GetMemoryArea(Addr);
    }

    public override string ToString()
    {
        return $"{Bank:X2}:{Addr:X4}";
    }

    public static GbPointer operator+(GbPointer p, int i)
    {
        return new GbPointer((ushort)(p.Addr + i), p.Bank);
    }

    public void AssertBanked()
    {
        Helper.Assert(IsBanked, $"GbPointer expected to be banked: {this}");
    }

    public static EmulatorMemoryArea GetMemoryArea(ushort addr)
    {
        if (addr < 0x4000) return EmulatorMemoryArea.ROM0;
        if (addr < 0x8000) return EmulatorMemoryArea.ROMX;
        if (addr < 0xa000) return EmulatorMemoryArea.VRAM;
        if (addr < 0xc000) return EmulatorMemoryArea.SRAM;
        if (addr < 0xd000) return EmulatorMemoryArea.WRAM0;
        if (addr < 0xe000) return EmulatorMemoryArea.WRAMX;
        if (addr < 0xf000) return EmulatorMemoryArea.WRAM0;
        if (addr < 0xfe00) return EmulatorMemoryArea.WRAMX;
        if (addr < 0xfea0) return EmulatorMemoryArea.OAM;
        if (addr < 0xff00) return EmulatorMemoryArea.INVALID;
        if (addr < 0xff80) return EmulatorMemoryArea.IO;
        if (addr < 0xffff) return EmulatorMemoryArea.HRAM;

        return EmulatorMemoryArea.IE;
    }
}

public readonly struct EmulatorStartOptions
{
    public bool Quickstart { get; init; }
    public int RoomIndex { get; init; }
    public Season Season { get; init; }
    public byte X { get; init; }
    public byte Y { get; init; }
}

/// <summary>
/// A gameboy system bus address maps to one of these values
/// </summary>
public enum EmulatorMemoryArea
{
    ROM0,
    ROMX,
    VRAM,
    SRAM,
    WRAM0,
    WRAMX,
    OAM,
    IO,
    HRAM,
    IE,
    INVALID,
}
