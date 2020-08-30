using System;

namespace LynnaLab {

// An exception resulting from some kind of unexpected thing in the disassembly (possibly caused by
// the user).
public class ProjectErrorException : Exception {
    public ProjectErrorException() {}
    public ProjectErrorException(string s) : base(s) {}
    public ProjectErrorException(string message, Exception inner)
        : base(message, inner) {}
}

// Used with Project, FileParser, etc. when trying to look up labels and such
public class InvalidLookupException : ProjectErrorException {
    public InvalidLookupException() {}
    public InvalidLookupException(string s) : base(s) {}
}

// Generic error resulting from unexpected things in the disassembly files (where there is
// definitely something unexpected and wrong with the disassembly itself).
public class AssemblyErrorException : ProjectErrorException {
    public AssemblyErrorException()
        : base() {}
    public AssemblyErrorException(string message)
        : base(message) {}
    public AssemblyErrorException(string message, Exception inner)
        : base(message, inner) {}
}

public class DuplicateLabelException : AssemblyErrorException {
    public DuplicateLabelException()
        : base() {}
    public DuplicateLabelException(string message)
        : base(message) {}
    public DuplicateLabelException(string message, Exception inner)
        : base(message, inner) {}
}

// Used by PngGfxStream when an image is formatted in an unexpected way
public class InvalidImageException : ProjectErrorException {
    public InvalidImageException() : base() {}
    public InvalidImageException(string s) : base(s) {}
    public InvalidImageException(Exception e) : base(e.Message) {}
}

// Used by Treasure class when you try to instantiate one that doesn't exist
public class InvalidTreasureException : ProjectErrorException {
    public InvalidTreasureException() : base() {}
    public InvalidTreasureException(string s) : base(s) {}
    public InvalidTreasureException(Exception e) : base(e.Message) {}
}

// Used by PaletteHeaderGroup class when you try to instantiate one that doesn't exist
public class InvalidPaletteHeaderGroupException : ProjectErrorException {
    public InvalidPaletteHeaderGroupException() : base() {}
    public InvalidPaletteHeaderGroupException(string s) : base(s) {}
    public InvalidPaletteHeaderGroupException(Exception e) : base(e.Message) {}
}

// Used by ObjectAnimation.cs and ObjectAnimationFrame.cs.
public class InvalidAnimationException : ProjectErrorException {
    public InvalidAnimationException() : base() {}
    public InvalidAnimationException(string s) : base(s) {}
    public InvalidAnimationException(Exception e) : base(e.Message) {}
}

// This is different from "InvalidAnimationException" because it's not really an error; the
// animation simply hasn't been defined.
public class NoAnimationException : InvalidAnimationException {
    public NoAnimationException() : base() {}
    public NoAnimationException(string s) : base(s) {}
    public NoAnimationException(Exception e) : base(e.Message) {}
}

}
