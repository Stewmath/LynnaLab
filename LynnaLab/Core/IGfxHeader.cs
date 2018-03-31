using System.IO;

namespace LynnaLab {

/// <summary>
///  Represents a "Gfx Header"; a reference to some gfx data as well as where that data should be
///  loaded to.
/// </summary>
public interface IGfxHeader {
    // May be null, if GfxStream is in use (source is from ROM)
    int? SourceAddr { get; }
    int? SourceBank { get; }

    // May be null, if source is from RAM
    Stream GfxStream { get; }

    // The number of blocks (16 bytes each) to be read.
    int BlockCount { get; }

    // True if the bit indicating that there is a next value is set.
    bool ShouldHaveNext { get; }
}
}
