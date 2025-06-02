using System;

namespace LynnaLib
{
    /// This basically consists of a list of "PaletteHeaderData"s. Keep in mind that some
    /// PaletteHeaderGroups are identical (reference the same data).
    public class PaletteHeaderGroup : ProjectIndexedDataType, IndexedProjectDataInstantiator
    {
        readonly PaletteHeaderData firstPaletteHeader;

        public event EventHandler<EventArgs> ModifiedEvent;


        public PaletteHeaderData FirstPaletteHeader
        {
            get { return firstPaletteHeader; }
        }

        /// Name of the label pointing to the data
        public string LabelName { get; private set; }

        /// Name of the constant alias (ie. PALH_40)
        public string ConstantAliasName
        {
            get
            {
                return Project.PaletteHeaderMapping.ByteToString(Index);
            }
        }

        private PaletteHeaderGroup(Project project, int index) : base(project, index)
        {
            try
            {
                LabelName = "paletteHeader" + index.ToString("x2");
                Data headerData = Project.GetData(LabelName);

                if (!(headerData is PaletteHeaderData))
                    throw new InvalidPaletteHeaderGroupException("Expected palette header group " + index.ToString("X") + " to start with palette header data");
                firstPaletteHeader = (PaletteHeaderData)headerData;
            }
            catch (InvalidLookupException e)
            {
                throw new InvalidPaletteHeaderGroupException(e.Message);
            }
            InstallEventHandlers();
        }

        static ProjectDataType IndexedProjectDataInstantiator.Instantiate(Project p, int index)
        {
            return new PaletteHeaderGroup(p, index);
        }

        // TODO: error handling
        public Color[][] GetObjPalettes()
        {
            Color[][] ret = new Color[8][];

            Foreach((palette) =>
            {
                Color[][] palettes = palette.GetPalettes();
                if (palette.PaletteType == PaletteType.Sprite)
                {
                    for (int i = 0; i < palette.NumPalettes; i++)
                    {
                        ret[i + palette.FirstPalette] = palettes[i];
                    }
                }
            });
            return ret;
        }

        public void Foreach(Action<PaletteHeaderData> action)
        {
            PaletteHeaderData palette = firstPaletteHeader;
            while (true)
            {
                action(palette);
                Data nextData = palette.NextData;
                if (nextData is PaletteHeaderData)
                {
                    palette = palette.NextData as PaletteHeaderData;
                    continue;
                }
                else if (nextData.CommandLowerCase == "m_paletteheaderend")
                {
                    break;
                }
                else
                {
                    throw new ProjectErrorException("Expected palette data to end with m_PaletteHeaderEnd");
                }
            }
        }


        void InstallEventHandlers()
        {
            Foreach((palette) =>
            {
                palette.PaletteDataModifiedEvent += (sender, args) =>
                {
                    ModifiedEvent?.Invoke(this, null);
                };
            });
        }
    }
}
