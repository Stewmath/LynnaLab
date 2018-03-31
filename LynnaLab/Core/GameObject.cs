using System.Collections.Generic;

namespace LynnaLab {

/// <summary>
///  An interface for "data/interactionData.s", "enemyData.s", "itemData.s", "partData.s". Mostly
///  contains information about their graphics. Read-only for now.
///
///  Differs from "ObjectData" in that this corresponds to the actual object as it's represented
///  in-game, while ObjectData is used for managing placement of objects (not the properties of the
///  objects themselves).
/// </summary>
public abstract class GameObject : ProjectIndexedDataType {

    // Using a dictionary because I don't know what the upper limit is to # of animations...
    Dictionary<int,ObjectAnimation> animations = new Dictionary<int,ObjectAnimation>();


    public GameObject(Project p, int index) : base(p, index) {
    }

    public byte ID {
        get { return (byte)(Index>>8); }
    }
    public byte SubID {
        get { return (byte)(Index&0xff); }
    }

    public abstract string TypeName { get; }


    /// <summary>
    ///  If an invalid object is specified, it won't have data. In this case, all fields below here
    ///  should be considered invalid.
    ///  However, it might still be considered valid if it ends up reading bytes from somewhere else
    ///  on accident.
    /// </summary>
    public abstract bool DataValid { get; }

    /// <summary>
    ///  The "object gfx header" used by this object.
    /// </summary>
    public abstract byte ObjectGfxHeaderIndex { get; }

    /// <summary>
    ///  The base tileindex for this object (relative to the graphics in the ObjectGfxHeader)
    /// </summary>
    public abstract byte TileIndexBase { get; }

    /// <summary>
    ///  The flags used by this object (ie. palette).
    /// </summary>
    public abstract byte OamFlagsBase { get; }

    /// <summary>
    ///  The default animation index.
    /// </summary>
    public abstract byte DefaultAnimationIndex { get; }


    public ObjectGfxHeaderData ObjectGfxHeaderData {
        get { return Project.GetObjectGfxHeaderData(ObjectGfxHeaderIndex); }
    }
    public ObjectAnimation DefaultAnimation {
        get { return GetAnimation(DefaultAnimationIndex); }
    }



    public ObjectAnimation GetAnimation(int i) {
        try {
            return animations[i];
        }
        catch(KeyNotFoundException) {
            var anim = new ObjectAnimation(this, i);
            animations[i] = anim;
            return anim;
        }
    }
}
}
