[assembly: CaptureConsole]
namespace LynnaLib.Tests;

using SameBoySharp;

public class TestEmulator
{
    public TestEmulator()
    {
    }

    [Fact]
    public static async Task TestOraclesEmulator()
    {
        OraclesEmulator emu = new OraclesEmulator(TestProject.ProjectBasePath + "/ages.gbc");
        emu.GB.SetLogCallback((s) => Console.WriteLine("SAMEBOY: " + s));

        // Symbols are working
        AssertEval(emu.GB, "begin", 0x150, 0x00);

        Assert.Equal(0xfe, emu.GB.SafeReadMemory(0xff50));

        emu.Dispose();
    }


    static void AssertEval(SameBoy gb, string str, ushort val, short bank)
    {
        Assert.False(gb.DebuggerEvaluate(str, out ushort v, out short b));
        Assert.Equal(val, v);
        Assert.Equal(bank, b);
    }
}
