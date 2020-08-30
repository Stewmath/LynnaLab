using System;
using System.Drawing;

namespace LynnaLab
{
    /// This basically consists of a list of "PaletteHeaderData"s. Keep in mind that some
    /// PaletteHeaderGroups are identical (reference the same data).
    public class PaletteHeaderGroup : ProjectIndexedDataType
    {
        PaletteHeaderData firstPaletteHeader;

        public event EventHandler<EventArgs> ModifiedEvent;


        public PaletteHeaderData FirstPaletteHeader {
            get { return firstPaletteHeader; }
        }

        /// Name of the label pointing to the data
        public string LabelName { get; private set; }

        /// Name of the constant alias (ie. PALH_40)
        public string ConstantAliasName {
            get {
                return Project.PaletteHeaderMapping.ByteToString(Index);
            }
        }

        PaletteHeaderGroup(Project project, int index) : base(project, index)
        {
            try {
                FileParser palettePointerFile = project.GetFileWithLabel("paletteHeaderTable");
                Data headerPointerData = palettePointerFile.GetData("paletteHeaderTable", index*2);
                LabelName = headerPointerData.GetValue(0);
                FileParser paletteHeaderFile = project.GetFileWithLabel(LabelName);
                Data headerData = paletteHeaderFile.GetData(headerPointerData.GetValue(0));

                if (!(headerData is PaletteHeaderData))
                    throw new InvalidPaletteHeaderGroupException("Expected palette header group " + index.ToString("X") + " to start with palette header data");
                firstPaletteHeader = (PaletteHeaderData)headerData;
            }
            catch (InvalidLookupException e) {
                throw new InvalidPaletteHeaderGroupException(e.Message);
            }
            InstallEventHandlers();
        }

        // TODO: error handling
        public Color[][] GetObjPalettes() {
            Color[][] ret = new Color[8][];

            Foreach((palette) => {
                Color[][] palettes = palette.GetPalettes();
                if (palette.PaletteType == PaletteType.Sprite) {
                    for (int i=0; i<palette.NumPalettes; i++) {
                        ret[i+palette.FirstPalette] = palettes[i];
                    }
                }
            });
            return ret;
        }

        public void Foreach(Action<PaletteHeaderData> action) {
            PaletteHeaderData palette = firstPaletteHeader;
            while (true) {
                action(palette);
                if (!palette.ShouldHaveNext())
                    break;
                palette = palette.NextData as PaletteHeaderData;
            }
        }


        void InstallEventHandlers() {
            Foreach((palette) => {
                palette.PaletteDataModifiedEvent += (sender, args) => {
                    ModifiedEvent?.Invoke(this, null);
                };
            });
        }
    }
}
