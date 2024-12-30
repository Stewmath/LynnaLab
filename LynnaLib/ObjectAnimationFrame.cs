using System;
using System.Collections.Generic;
using System.IO;

namespace LynnaLib
{

    /// <summary>
    ///  This will throw an "InvalidAnimationException" whenever something unexpected happens (which
    ///  seems common, particularly when animations are undefined for an object).
    ///
    ///  NOTE: An instance of this class does not necessarily uniquely point to the data in
    ///  question, as multiple objects may point to the same data.
    /// </summary>
    public class ObjectAnimationFrame
    {
        readonly ObjectAnimation _animation;
        readonly Data _animData;
        Data _oamData;

        public ObjectAnimationFrame(ObjectAnimation anim, Data animData)
        {
            _animation = anim;
            _animData = animData;

            GenerateBitmaps();

            var gfxStream = _animation.ObjectGfxHeaderData.GfxStream;
            if (gfxStream is ReloadableStream)
            {
                (gfxStream as ReloadableStream).ExternallyModifiedEvent += (s, a) =>
                {
                    GenerateBitmaps();
                };
            }
        }

        void GenerateBitmaps()
        {
            if (bitmaps != null)
            {
                foreach (var tup in bitmaps)
                {
                    tup.Item1.Dispose();
                }
            }

            bitmaps = new List<Tuple<Bitmap, int, int>>();
            Color[][] palettes = _animation.GetPalettes();

            try
            {
                ObjectGfxHeaderData gfxHeader = _animation.ObjectGfxHeaderData;

                // Note: this "index" is more like index*2, since it's added directly to the offset
                // without being multiplied first
                int oamIndex = (byte)_animData.NextData.GetIntValue(0);

                // Get entry in oam table for this object
                Data oamPtr = Project.GetData(_animation.OamTableName, oamIndex);
                _oamData = Project.GetData(oamPtr.GetValue(0));

                // Load sprite images
                int _numSprites = _oamData.GetIntValue(0);
                Data data = _oamData.NextData;

                for (int i = 0; i < _numSprites; i++)
                {
                    int y = (sbyte)data.GetIntValue(0) - 16;
                    data = data.NextData;
                    int x = (sbyte)data.GetIntValue(0) - 8;
                    data = data.NextData;
                    int tileIndex = data.GetIntValue(0) + _animation.TileIndexBase;
                    data = data.NextData;
                    byte flags = (byte)(data.GetIntValue(0) | _animation.OamFlagsBase);
                    data = data.NextData;

                    int origTileIndex = tileIndex;

                    while (tileIndex >= 0x20)
                    {
                        if (gfxHeader.ShouldHaveNext)
                        {
                            gfxHeader = gfxHeader.NextGfxHeader;
                            tileIndex -= 0x20;
                        }
                        else
                            throw new InvalidAnimationException("Tileindex out of range (" + tileIndex + ")");
                    }

                    int tileOffset = (tileIndex & 0xfe) * 16;
                    Stream gfxStream = gfxHeader.GfxStream;

                    if (gfxStream.Length - tileOffset < 0x20)
                        throw new InvalidAnimationException("Sprite not defined in gfx data");

                    gfxStream.Seek(tileOffset, SeekOrigin.Begin);
                    byte[] gfxData = new byte[0x20];
                    gfxStream.Read(gfxData, 0, 0x20);

                    Bitmap bitmap = GbGraphics.RawTileToBitmap(gfxData, palettes[flags & 7], flags);
                    bitmaps.Add(new Tuple<Bitmap, int, int>(bitmap, x, y));
                }
            }
            catch (InvalidLookupException e)
            {
                bitmaps = null;
                throw new InvalidAnimationException(e);
            }
            catch (FormatException e)
            {
                bitmaps = null;
                throw new InvalidAnimationException(e);
            }
        }


        public Project Project
        {
            get { return _animation.Project; }
        }

        public Data AnimDataStart
        {
            get { return _animData; }
        }

        List<Tuple<Bitmap, int, int>> bitmaps;

        // Number of frames this lasts
        public byte Length
        {
            get { return (byte)_animData.GetIntValue(0); }
        }


        /// <summary>
        /// Loops through all 8x16 sprite images and calls "spriteDrawer" on all of them so that the
        /// full image can be drawn. Done in reverse order so that layering can work properly.
        ///
        /// Will throw InvalidAnimationException if initialization failed earlier...
        /// </summary>
        public void Draw(Action<Bitmap, int, int> spriteDrawer)
        {
            if (bitmaps == null)
                throw new InvalidAnimationException();

            int _numSprites = _oamData.GetIntValue(0);

            // Draw sprites in backwards order to respect priority properly
            for (int i = _numSprites - 1; i >= 0; i--)
            {
                Tuple<Bitmap, int, int> tup = bitmaps[i];
                spriteDrawer(tup.Item1, tup.Item2, tup.Item3);
            }
        }


        // (TODO: when implemented, make this return null sometimes? Not all animations loop, but
        // there's not an easy way to determine when one ends...)
        public ObjectAnimationFrame GetNextFrame()
        {
            throw new NotImplementedException();
        }
    }
}
