using System;

namespace LynnaLab {


// Used with Project, FileParser, etc. when trying to look up labels and such
public class InvalidLookupException : Exception {
    public InvalidLookupException() {}
    public InvalidLookupException(string s) : base(s) {}
}

public class DuplicateLabelException : Exception {
    public DuplicateLabelException()
        : base() {}
    public DuplicateLabelException(string message)
        : base(message) {}
    public DuplicateLabelException(string message, Exception inner)
        : base(message, inner) {}
}

// Used by ObjectAnimation.cs and ObjectAnimationFrame.cs
public class InvalidAnimationException : Exception {
    public InvalidAnimationException() : base() {}
    public InvalidAnimationException(string s) : base(s) {}
    public InvalidAnimationException(Exception e) : base(e.Message) {}
}

}
