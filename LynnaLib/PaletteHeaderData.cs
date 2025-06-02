using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LynnaLib
{
    public enum PaletteType
    {
        Background = 0,
        Sprite
    };

    // Class represents macro:
    // m_PaletteHeader[Bg|Spr] startIndex numPalettes address
    //                          0           1           2
    public class PaletteHeaderData : Data
    {
        // Variables

        class PaletteHeaderState : Data.DataState
        {
            [JsonRequired]
            public InstanceResolver<FileParser> PaletteDataFile; // Can be null
            public bool IsResolvable;

            public override void CaptureInitialState(FileComponent parent)
            {
                parent.Project.TransactionManager.CaptureInitialState<PaletteHeaderState>(parent);
            }
        }


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
                Debug.Assert(State.PaletteDataFile != null);
                return (RgbData)State.PaletteDataFile?.Instance.GetData(GetValue(2));
            }
        }

        public PaletteType PaletteType
        {
            get { return CommandLowerCase == "m_paletteheaderbg" ? PaletteType.Background : PaletteType.Sprite; }
        }
        public int FirstPalette
        {
            get { return Project.Eval(GetValue(0)); }
        }
        public int NumPalettes
        {
            get { return Project.Eval(GetValue(1)); }
        }
        public string PointerName
        {
            get { return GetValue(2); }
        }

        // Sometimes palette headers reference RAM data instead of ROM data. In that case there is
        // no way to resolve the palette data.
        public bool IsResolvable
        {
            get { return State.IsResolvable; }
        }

        private PaletteHeaderState State { get { return state as PaletteHeaderState; } }


        public PaletteHeaderData(Project p, string id,string command, IEnumerable<string> values, FileParser parser, IList<string> spacing)
            : base(p, id, command, values, 3, parser, spacing, () => new PaletteHeaderState())
        {

            int dest;
            if (!Project.TryEval(GetValue(2), out dest))
                dest = -1;

            string labelName = GetValue(2);
            if (dest != -1 || !Project.HasLabel(labelName))
                State.IsResolvable = false;
            else
            {
                State.IsResolvable = true;
                State.PaletteDataFile = new(Project.GetFileWithLabel(labelName));
                if (!(State.PaletteDataFile?.Instance.GetData(labelName) is RgbData))
                    throw new Exception("Label \"" + labelName + "\" was expected to reference data defined with m_RGB16");
            }

            CommonInitialization();
        }

        /// <summary>
        /// State-based constructor, for network transfer (located via reflection)
        /// </summary>
        private PaletteHeaderData(Project p, string id, TransactionState state)
            : base(p, id, state)
        {
        }

        void CommonInitialization()
        {
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

        public Color GetColor(int palette, int colorIndex)
        {
            RgbData data = GetRgbData(palette, colorIndex);
            return data.Color;
        }

        RgbData GetRgbData(int palette, int colorIndex)
        {
            if (!IsResolvable)
                throw new Exception($"Palette {PointerName} could not be resolved");

            if (palette < FirstPalette || palette >= FirstPalette + NumPalettes)
                throw new Exception($"Requested palette index {palette} from palette header, out of range");

            palette -= FirstPalette;
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
                if (data == null)
                    throw new Exception("PaletteData missing expected RgbData?");
                action(data);
                data = data.NextData as RgbData;
            }
        }


        public override void OnInitializedFromTransfer()
        {
            CommonInitialization();
        }
    }

}
