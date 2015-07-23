using System.Drawing;
using System.Collections.Generic;

namespace LynnaLab
{
	public class Data 
	{
		// Command is like .db, .dw, or a macro.
		string command;
		List<string> values;
		// Size in bytes
		// -1 if indeterminate?
		protected int size;

		Data nextData;

		protected Project project;

		public string Command {
			get { return command; }
		}
		public IList<string> Values {
			get { return values.AsReadOnly(); }
		}
		public int Size {
			get { return size; }
		}
		public Data Next {
			get { return nextData; }
			set { nextData = value; }
		}

		public Data(Project p, string command, IList<string> values, int size) {
			this.project = p;
			this.command = command;
			this.values = new List<string>(values);
			this.size = size;
		}
	}

    public class RgbData : Data {

        public Color Color {
            get {
                return Color.FromArgb(project.EvalToInt(Values[0]),
                        project.EvalToInt(Values[1]),
                        project.EvalToInt(Values[2]));
            }
        }

        public RgbData(Project p, string command, IList<string> values)
            : base(p, command, values, 2) {
        }
    }
}
