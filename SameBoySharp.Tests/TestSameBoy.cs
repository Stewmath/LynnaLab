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

        // load boot ROM prior to running anything
        gb.LoadBootRom(CgbBios);

        // Test memory access and BIOS was loaded
        Assert.Equal(0x31, gb.SafeReadMemory(0));

        gb.Dispose();
    }


    static void AssertEval(SameBoy gb, string str, ushort val, short bank)
    {
        Assert.True(gb.DebuggerEvaluate(str, out ushort v, out short b));
        Assert.Equal(val, v);
        Assert.Equal(bank, b);
    }
}
