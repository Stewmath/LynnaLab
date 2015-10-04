using System;

namespace LynnaLab {

    public class LabelNotFoundException : Exception {

        public LabelNotFoundException() {}
        public LabelNotFoundException(string s) : base(s) {}
    }

	public class DuplicateLabelException : Exception {
		public DuplicateLabelException()
			: base() {}
	    public DuplicateLabelException(string message)
			: base(message) {}
	    public DuplicateLabelException(string message, Exception inner)
			: base(message, inner) {}
	}
}
