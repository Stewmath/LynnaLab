using System;

namespace LynnaLab
{

/// <summary>
///  An interaction object. The "index" is the full ID (2 bytes, including subid).
/// </summary>
public class EnemyObject : GameObject {

    Data objectData;

    byte _objectGfxHeaderIndex;
    byte _collisionReactionSet;
    byte _tileIndexBase;
    byte _oamFlagsBase;

    internal EnemyObject(Project p, int i) : base(p, i) {
        try {
            objectData = p.GetData("enemyData", ID*4);

            _objectGfxHeaderIndex = (byte)objectData.GetIntValue(0);
            _collisionReactionSet = (byte)objectData.GetIntValue(1);

            byte lookupIndex; // TODO: use this
            byte b3;

            if (objectData.GetNumValues() == 4) {
                lookupIndex = (byte)objectData.GetIntValue(2);
                b3 = (byte)(objectData.GetIntValue(3));
            }
            else {
                Data subidData = Project.GetData(objectData.GetValue(2));
                int count = SubID;
                while (count>0 && (subidData.GetIntValue(0)&0x80) == 0x80) {
                    subidData = subidData.NextData;
                    subidData = subidData.NextData;
                    count--;
                }
                lookupIndex = (byte)subidData.GetIntValue(0);
                b3 = (byte)(subidData.NextData.GetIntValue(0));
            }

            _tileIndexBase = (byte)((b3&0xf)*2);
            _oamFlagsBase = (byte)(b3>>4);
        }
        catch(InvalidLookupException e) {
            Console.WriteLine(e.ToString());
            objectData = null;
        }
        catch(FormatException e) {
            Console.WriteLine(e.ToString());
            objectData = null;
        }
    }


    // GameObject properties
    public override string TypeName {
        get { return "Enemy"; }
    }

    public override ConstantsMapping IDConstantsMapping {
        get { return Project.EnemyMapping; }
    }


    public override bool DataValid {
        get { return objectData != null; }
    }

    public override byte ObjectGfxHeaderIndex {
        get { return _objectGfxHeaderIndex; }
    }
    public override byte TileIndexBase {
        get { return _tileIndexBase; }
    }
    public override byte OamFlagsBase {
        get { return _oamFlagsBase; }
    }
    public override byte DefaultAnimationIndex {
        get { return 0; }
    }
}
}
