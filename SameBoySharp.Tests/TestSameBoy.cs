[assembly: CaptureConsole]
namespace LynnaLib.Tests;

using SameBoySharp;

public class TestSameBoy
{
    static string CgbBios = "bios/cgb_boot.bin";

    static string TestRomNibbles = "roms/nibbles.gb";

    public TestSameBoy()
    {
    }

    [Fact]
    public static async Task TestNibbles()
    {
        SameBoy gb = new SameBoy(SameBoy.Model.GB_MODEL_CGB_E);

        // For debugging tests
        gb.SetLogCallback((s) => Console.WriteLine("SAMEBOY: " + s));

        // We specified a CGB model
        Assert.True(gb.IsCgb());

        gb.LoadRom(TestRomNibbles);

        // Read the ROM successfully
        Assert.Equal("nibbles", gb.GetRomTitle());

        // Debugger expressions work
        AssertEval(gb, "1", 1, -1);
        AssertEval(gb, "2 + 2", 4, -1);

        // Symbol file can be loaded
        gb.DebuggerLoadSymbolFile(Path.ChangeExtension(TestRomNibbles, ".sym"));
        AssertEval(gb, "init", 0x21f, 0x00);

        // Boot ROM can be loaded
        gb.LoadBootRom(CgbBios);

        // Test memory access and BIOS was loaded
        Assert.Equal(0x31, gb.SafeReadMemory(0));

        // WriteMemory, ReadMemory
        gb.WriteMemory(0xd000, 0xdb);
        //Assert.Equal(0xdb, gb.ReadMemory(0xd000));
        Assert.Equal(0xdb, gb.SafeReadMemory(0xd000));

        gb.SetRenderingDisabled(true);

        // Test execution, register access
        Assert.Equal(0, gb.Registers.PC);
        gb.Run();
        Assert.Equal(3, gb.Registers.PC);

        // Register assignment works
        gb.Registers.AF = 0x1234;
        Assert.Equal(0x12, gb.Registers.A);
        Assert.Equal(0x34, gb.Registers.F);
        gb.Registers.BC = 0x5678;
        Assert.Equal(0x56, gb.Registers.B);
        Assert.Equal(0x78, gb.Registers.C);
        gb.Registers.DE = 0x9abc;
        Assert.Equal(0x9a, gb.Registers.D);
        Assert.Equal(0xbc, gb.Registers.E);
        gb.Registers.HL = 0xdef0;
        Assert.Equal(0xde, gb.Registers.H);
        Assert.Equal(0xf0, gb.Registers.L);

        // Direct access
        Assert.Equal(1, gb.GetDirectAccess(SameBoy.GBDirectAccess.IE).Length);

        gb.Dispose();
    }

    [Fact]
    public static void TestExecutionCallback()
    {
        LoadRom(out SameBoy gb, TestRomNibbles);

        bool executed = false;
        gb.DebuggerEvaluate("init", out ushort initAddr, out _);

        var callback = (ushort addr, byte opcode) =>
        {
            if (addr == initAddr)
                executed = true;
        };

        gb.SetExecutionCallback(callback);

        while (gb.Registers.PC != initAddr)
            gb.Run();

        Assert.False(executed);
        gb.Run();
        Assert.True(executed);
    }


    static void AssertEval(SameBoy gb, string str, ushort val, short bank)
    {
        Assert.False(gb.DebuggerEvaluate(str, out ushort v, out short b));
        Assert.Equal(val, v);
        Assert.Equal(bank, b);
    }

    static void LoadRom(out SameBoy gb, string rom)
    {
        gb = new SameBoy(SameBoy.Model.GB_MODEL_CGB_E);
        gb.LoadRom(rom);
        gb.LoadBootRom(CgbBios);
        gb.DebuggerLoadSymbolFile(Path.ChangeExtension(rom, ".sym"));
        gb.SetRenderingDisabled(true);
    }
}
