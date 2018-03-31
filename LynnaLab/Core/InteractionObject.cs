using System;

namespace LynnaLab
{

/// <summary>
///  An interaction object. The "index" is the full ID (2 bytes, including subid).
/// </summary>
public class InteractionObject : GameObject {

    Data objectData;

    byte b0,b1,b2;

    internal InteractionObject(Project p, int i) : base(p, i) {
        try {
            objectData = p.GetData("interactionData", ID*3);

            // If this points to more data, follow the pointer
            if (objectData.GetNumValues() == 1) {
                string label = objectData.GetValue(0);
                objectData = p.GetData(label);

                int count = SubID;
                while (count>0 && (objectData.GetIntValue(1)&0x80) == 0) {
                    count--;
                    objectData = objectData.NextData;
                }
            }

            b0 = (byte)objectData.GetIntValue(0);
            b1 = (byte)objectData.GetIntValue(1);
            b2 = (byte)objectData.GetIntValue(2);

            if (b0 == 0) // NpcGfxHeaderIndex is 0
                objectData = null;
        }
        catch(InvalidLookupException) {
            objectData = null;
        }
        catch(FormatException) {
            objectData = null;
        }
    }


    // GameObject properties
    public override string TypeName {
        get { return "Interaction"; }
    }


    public override bool DataValid {
        get { return objectData != null; }
    }

    public override byte NpcGfxHeaderIndex {
        get { return b0; }
    }
    public override byte TileIndexBase {
        get { return (byte)(b1&0x7f); }
    }
    public override byte OamFlagsBase {
        get { return (byte)((b2>>4)&7); }
    }
    public override byte DefaultAnimationIndex {
        get { return (byte)(b2&0xf); }
    }
}
}
