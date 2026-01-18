[assembly: CaptureConsole]
namespace LynnaLib.Tests;

using SameBoySharp;

public class TestSameBoy
{
    public TestSameBoy()
    {
    }

    [Fact]
    public static async Task TestSameBoySharp()
    {
        SameBoy gb = new SameBoy(SameBoy.Model.GB_MODEL_CGB_E);

        gb.SetLogCallback((s) => Console.WriteLine("SAMEBOY: " + s));

        gb.LoadRom(TestProject.ProjectBasePath + "/seasons.gbc");

        // Read the ROM successfully
        Assert.Equal("ZELDA DIN", gb.GetRomTitle());

        // Debugger expressions work
        AssertEval(gb, "1", 1, -1);
        AssertEval(gb, "2 + 2", 4, -1);

        // Symbol file can be loaded
        gb.DebuggerLoadSymbolFile(TestProject.ProjectBasePath + "/seasons.sym");
        AssertEval(gb, "begin", 0x150, 0x00);

        // load boot ROM prior to running anything
        gb.LoadBootRom("cgb_boot.bin");

        gb.Dispose();
    }


    static void AssertEval(SameBoy gb, string str, ushort val, short bank)
    {
        Assert.True(gb.DebuggerEvaluate(str, out ushort v, out short b));
        Assert.Equal(val, v);
        Assert.Equal(bank, b);
    }
}
