using System;

namespace LynnaLab {

// Used with Project, FileParser, etc. when trying to look up labels and such
public class InvalidLookupException : Exception {
    public InvalidLookupException() {}
    public InvalidLookupException(string s) : base(s) {}
}

// Generic error resulting from unexpected things in the disassembly files.
public class AssemblyErrorException : Exception {
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
public class InvalidImageException : Exception {
    public InvalidImageException() : base() {}
    public InvalidImageException(string s) : base(s) {}
    public InvalidImageException(Exception e) : base(e.Message) {}
}

// Used by ObjectAnimation.cs and ObjectAnimationFrame.cs.
public class InvalidAnimationException : Exception {
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
