using System.Collections.Generic;
using System.Linq;

namespace LynnaLib
{

    /// <summary>
    ///  Represents an animation for an object.
    ///
    /// If the animation doesn't loop, there's currently no way to know when it stops, which is
    /// problematic...
    ///
    /// This will throw an "InvalidAnimationException" whenever something unexpected happens (which
    /// seems common, particularly when animations are undefined for an object).
    /// </summary>
    public class ObjectAnimation
    {
        readonly GameObject _gameObject;
        readonly int _animationIndex;
        readonly Data _animationData;

        List<ObjectAnimationFrame> loadedFrames = new List<ObjectAnimationFrame>();

        public Project Project
        {
            get { return _gameObject.Project; }
        }

        public string AnimationTableName
        {
            get
            {
                string s = _gameObject.TypeName.ToLower() + "AnimationTable";
                return Project.GetData(s, _gameObject.ID * 2).GetValue(0);
            }
        }
        public string OamTableName
        {
            get
            {
                string s = _gameObject.TypeName.ToLower() + "OamDataTable";
                return Project.GetData(s, _gameObject.ID * 2).GetValue(0);
            }
        }

        public ObjectGfxHeaderData ObjectGfxHeaderData
        {
            get { return _gameObject.ObjectGfxHeaderData; }
        }
        public byte TileIndexBase
        {
            get { return _gameObject.TileIndexBase; }
        }
        public byte OamFlagsBase
        {
            get { return _gameObject.OamFlagsBase; }
        }

        public ObjectAnimation(GameObject gameObject, int animationIndex)
        {
            _gameObject = gameObject;
            _animationIndex = animationIndex;

            if (!_gameObject.DataValid)
                throw new InvalidAnimationException();
            if (_gameObject.ObjectGfxHeaderIndex == 0)
                throw new NoAnimationException();

            try
            {
                _animationData = Project.GetData(Project.GetData(AnimationTableName, animationIndex * 2).GetValue(0));
            }
            catch (InvalidLookupException e)
            {
                throw new InvalidAnimationException(e);
            }
        }


        public ObjectAnimationFrame GetFrame(int i)
        {
            if (i < loadedFrames.Count)
                return loadedFrames[i];

            if (loadedFrames.Count == 0)
                loadedFrames.Add(new ObjectAnimationFrame(this, _animationData));

            try
            {
                Data data = loadedFrames.Last().AnimDataStart;

                while (true)
                {
                    if (i < loadedFrames.Count)
                        return loadedFrames[i];

                    data = data.NextData;
                    data = data.NextData;
                    data = data.NextData;

                    var frame = new ObjectAnimationFrame(this, data);
                    loadedFrames.Add(frame);
                }
            }
            catch (InvalidLookupException e)
            {
                throw new InvalidAnimationException(e);
            }
        }

        /// <summary>
        ///  Returns the array of palettes (8 palettes of 4 colors each) in use.
        ///
        ///  If a particular palette is undefined, it will be null (ie. palette[i] == null)
        /// </summary>
        public Color[][] GetPalettes()
        {
            Color[][] palettes = new Color[8][];
            Color[][] standardPal = Project.GetStandardSpritePalettes();
            Color[][] customPal = _gameObject.GetCustomPalettes();

            for (int i = 0; i < 6; i++)
            {
                palettes[i] = standardPal[i];
            }

            if (customPal == null)
                return palettes;

            for (int i = 0; i < 8; i++)
            {
                if (customPal[i] != null)
                    palettes[i] = customPal[i];
            }
            return palettes;
        }
    }

}
