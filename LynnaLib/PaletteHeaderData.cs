using System;
using System.Collections.Generic;

namespace LynnaLib
{
    public enum PaletteType
    {
        Background = 0,
        Sprite
    };

    // Class represents macro:
    // m_PaletteHeader[Bg|Spr] startIndex numPalettes address continue
    //                          0           1           2       3
    public class PaletteHeaderData : Data
    {
        FileParser paletteDataFile;


        /// This is DIFFERENT from the base class's "ModifiedEvent" because this triggers when the
        /// underlying palette data is modified, rather than when the metadata (pointers, etc) is
        /// modified!
        public event EventHandler<EventArgs> PaletteDataModifiedEvent;


        public RgbData Data
        {
            get
            {
                if (!IsResolvable)
                    return null;
                return (RgbData)paletteDataFile.GetData(GetValue(2));
            }
        }

        public PaletteType PaletteType
        {
            get { return CommandLowerCase == "m_paletteheaderbg" ? PaletteType.Background : PaletteType.Sprite; }
        }
        public int FirstPalette
        {
            get { return Project.EvalToInt(GetValue(0)); }
        }
        public int NumPalettes
        {
            get { return Project.EvalToInt(GetValue(1)); }
        }
        public string PointerName
        {
            get { return GetValue(2); }
        }

        // Sometimes palette headers reference RAM data instead of ROM data. In that case there is
        // no way to resolve the palette data.
        public bool IsResolvable
        {
            get; private set;
        }


        public PaletteHeaderData(Project p, string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, command, values, 3, parser, spacing)
        {

            int dest = -1;
            try
            {
                dest = Project.EvalToInt(GetValue(2));
            }
            catch (FormatException)
            {
                dest = -1;
            }

            if (dest != -1)
                IsResolvable = false;
            else
            {
                IsResolvable = true;
                paletteDataFile = Project.GetFileWithLabel(GetValue(2));
                if (!(paletteDataFile.GetData(GetValue(2)) is RgbData))
                    throw new Exception("Label \"" + GetValue(2) + "\" was expected to reference data defined with m_RGB16");
            }

            if (IsResolvable)
            {
                Foreach((rgbData) =>
                {
                    rgbData.ModifiedEvent += (sender, args) => PaletteDataModifiedEvent?.Invoke(this, null);
                });
            }
        }

        public Color[][] GetPalettes()
        {
            Color[][] ret = new Color[NumPalettes][];

            RgbData data = Data;

            for (int i = 0; i < NumPalettes; i++)
            {
                ret[i] = new Color[4];
                for (int j = 0; j < 4; j++)
                {
                    ret[i][j] = data.Color;
                    data = data.NextData as RgbData;
                }
            }

            return ret;
        }

        public void SetColor(int palette, int colorIndex, Color color)
        {
            RgbData data = GetRgbData(palette, colorIndex);
            data.Color = color;
        }

        public bool ShouldHaveNext()
        {
            return (Project.EvalToInt(GetValue(3)) & 0x80) == 0x80;
        }

        RgbData GetRgbData(int palette, int colorIndex)
        {
            RgbData data = Data;
            for (int i = 0; i < palette * 4 + colorIndex; i++)
            {
                data = data.NextData as RgbData;
            }
            return data;
        }

        void Foreach(Action<RgbData> action)
        {
            RgbData data = Data;
            for (int i = 0; i < NumPalettes * 4; i++)
            {
                action(data);
                data = data.NextData as RgbData;
            }
        }
    }

}
