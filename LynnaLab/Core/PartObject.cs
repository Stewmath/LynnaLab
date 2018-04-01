using System;

namespace LynnaLab
{

/// <summary>
///  An interaction object. The "index" is the full ID (2 bytes, including subid).
/// </summary>
public class PartObject : GameObject {

    Data objectData;

    byte _objectGfxHeaderIndex;
    byte _tileIndexBase;
    byte _oamFlagsBase;

    internal PartObject(Project p, int i) : base(p, i) {
        try {
            objectData = p.GetData("partData", ID*8);

            Data data = objectData;

            _objectGfxHeaderIndex = (byte)data.GetIntValue(0);
            for (int j=0;j<5;j++)
                data = data.NextData;
            _tileIndexBase = (byte)data.GetIntValue(0);
            data = data.NextData;
            _oamFlagsBase = (byte)data.GetIntValue(0);
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
        get { return "Part"; }
    }

    public override ConstantsMapping IDConstantsMapping {
        get { return Project.PartMapping; }
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
