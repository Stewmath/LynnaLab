using System;

namespace LynnaLab {

/// <summary>
///  Represents an animation for an object.
///
/// If the animation doesn't loop, there's currently no way to know when it stops, which is
/// problematic...
///
/// This will throw an "InvalidAnimationException" whenever something unexpected happens (which
/// seems common, particularly when animations are undefined for an object).
/// </summary>
public class ObjectAnimation {
    GameObject _gameObject;
    int _animationIndex;

    Data _animationData;

    public Project Project {
        get { return _gameObject.Project; }
    }

    public string AnimationTableName {
        get {
            string s = _gameObject.TypeName.ToLower() + "AnimationTable";
            return Project.GetData(s, _gameObject.ID*2).GetValue(0);
        }
    }
    public string OamTableName {
        get {
            string s = _gameObject.TypeName.ToLower() + "OamDataTable";
            return Project.GetData(s, _gameObject.ID*2).GetValue(0);
        }
    }

    public ObjectGfxHeaderData ObjectGfxHeaderData {
        get { return _gameObject.ObjectGfxHeaderData; }
    }
    public byte TileIndexBase {
        get { return _gameObject.TileIndexBase; }
    }
    public byte OamFlagsBase {
        get { return _gameObject.OamFlagsBase; }
    }

    public ObjectAnimation(GameObject gameObject, int animationIndex) {
        _gameObject = gameObject;
        _animationIndex = animationIndex;

        if (!_gameObject.DataValid)
            throw new InvalidAnimationException();

        try {
            _animationData = Project.GetData(Project.GetData(AnimationTableName, animationIndex*2).GetValue(0));
        }
        catch(InvalidLookupException e) {
            throw new InvalidAnimationException(e);
        }
    }


    public ObjectAnimationFrame GetFrame(int i) {
        // TODO: cache
        try {
            Data data = _animationData;
            for (int j=0; j<i; j++) {
                data = data.NextData;
                data = data.NextData;
                data = data.NextData;
            }
            return new ObjectAnimationFrame(this, data);
        }
        catch(InvalidLookupException e) {
            throw new InvalidAnimationException(e);
        }
    }
}

}
