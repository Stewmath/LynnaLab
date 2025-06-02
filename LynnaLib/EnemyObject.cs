using System;

namespace LynnaLib
{

    /// <summary>
    ///  An enemy object. The "index" is the full ID (2 bytes, including subid).
    /// </summary>
    public class EnemyObject : GameObject, IndexedProjectDataInstantiator
    {

        readonly Data objectData;

        readonly byte _objectGfxHeaderIndex;
        readonly byte _collisionReactionSet;
        readonly byte _tileIndexBase;
        readonly byte _oamFlagsBase;

        private EnemyObject(Project p, int i) : base(p, i)
        {
            try
            {
                if ((i >> 8) >= 0x80)
                    throw new ProjectErrorException($"Invalid enemy index {i:x2}");

                objectData = p.GetData("enemyData", ID * 4);

                _objectGfxHeaderIndex = (byte)objectData.GetIntValue(0);
                _collisionReactionSet = (byte)objectData.GetIntValue(1);

                byte lookupIndex; // TODO: use this
                byte b3;

                if (objectData.GetNumValues() == 4)
                {
                    lookupIndex = (byte)objectData.GetIntValue(2);
                    b3 = (byte)(objectData.GetIntValue(3));
                }
                else
                {
                    Data subidData = Project.GetData(objectData.GetValue(2));
                    int count = SubID;

                    // If this points to more data, follow the pointer
                    while (count > 0)
                    {
                        count--;
                        var next = subidData.NextData;
                        if (next.CommandLowerCase == "m_enemysubiddata")
                        {
                            subidData = next;
                            continue;
                        }
                        else if (next.CommandLowerCase == "m_enemysubiddataend")
                        {
                            break;
                        }
                        else
                        {
                            throw new ProjectErrorException("Enemy Subid data ended unexpectedly");
                        }
                    }
                    lookupIndex = (byte)subidData.GetIntValue(0);
                    b3 = (byte)(subidData.GetIntValue(1));
                }

                _tileIndexBase = (byte)((b3 & 0xf) * 2);
                _oamFlagsBase = (byte)(b3 >> 4);
            }
            catch (InvalidLookupException e)
            {
                Console.WriteLine(e.ToString());
                objectData = null;
            }
            catch (FormatException e)
            {
                Console.WriteLine(e.ToString());
                objectData = null;
            }
        }

        static ProjectDataType IndexedProjectDataInstantiator.Instantiate(Project p, int index)
        {
            return new EnemyObject(p, index);
        }


        // GameObject properties
        public override string TypeName
        {
            get { return "Enemy"; }
        }

        public override ConstantsMapping IDConstantsMapping
        {
            get { return Project.EnemyMapping; }
        }


        public override bool DataValid
        {
            get { return objectData != null; }
        }

        public override byte ObjectGfxHeaderIndex
        {
            get { return _objectGfxHeaderIndex; }
        }
        public override byte TileIndexBase
        {
            get { return _tileIndexBase; }
        }
        public override byte OamFlagsBase
        {
            get { return _oamFlagsBase; }
        }
        public override byte DefaultAnimationIndex
        {
            get { return 0; }
        }
    }
}
