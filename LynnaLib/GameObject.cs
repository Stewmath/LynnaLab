namespace LynnaLib
{

    /// <summary>
    ///  An interface for "data/interactionData.s", "enemyData.s", "itemData.s", "partData.s". Mostly
    ///  contains information about their graphics. Read-only for now.
    ///
    ///  Differs from "ObjectData" in that this corresponds to the actual object as it's represented
    ///  in-game, while ObjectData is used for managing placement of objects (not the properties of the
    ///  objects themselves).
    /// </summary>
    public abstract class GameObject : ProjectIndexedDataType
    {

        // Using a dictionary because I don't know what the upper limit is to # of animations...
        Dictionary<int, ObjectAnimation> _animations = new Dictionary<int, ObjectAnimation>();


        public GameObject(Project p, int index) : base(p, index)
        {
        }

        public byte ID
        {
            get { return (byte)(Index >> 8); }
        }
        public byte SubID
        {
            get { return (byte)(Index & 0xff); }
        }

        public abstract string TypeName { get; }

        /// <summary>
        ///  The ConstantsMapping for the Main ID.
        /// </summary>
        public abstract ConstantsMapping IDConstantsMapping { get; }


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


        public ObjectGfxHeaderData ObjectGfxHeaderData
        {
            get { return Project.GetObjectGfxHeaderData(ObjectGfxHeaderIndex); }
        }
        public ObjectAnimation DefaultAnimation
        {
            get { return GetAnimation(DefaultAnimationIndex); }
        }


        public Color[][] GetCustomPalettes()
        {
            string field = GetSubIDDocumentation()?.GetField("palette");
            if (field == null)
                return null;

            int paletteIndex;
            if (!Project.TryEval(field, out paletteIndex))
                return null;
            return Project.GetIndexedDataType<PaletteHeaderGroup>(paletteIndex).GetObjPalettes();
        }

        public ObjectAnimation GetAnimation(int i)
        {
            if (_animations.ContainsKey(i))
                return _animations[i];

            var anim = new ObjectAnimation(this, i);
            _animations[i] = anim;
            return anim;
        }

        /// <summary>
        ///  Returns the documentation for the object with this ID (not specific to subID).
        /// </summary>
        public Documentation GetIDDocumentation()
        {
            Documentation doc = IDConstantsMapping.GetDocumentationForValue(ID);

            if (doc == null)
                return null;

            var keys = new HashSet<string>(doc.Keys);
            foreach (string key in keys)
            {
                if (key.Length >= 6 && key.Substring(0, 6) == "subid_")
                {
                    string subidName = key.Substring(6);
                    doc.SetField(subidName, doc.GetSubDocumentation(key).GetField("desc"));
                }
                else
                {
                    doc.Description += "\n\n" + key + ": " + doc.GetField(key);
                    doc.RemoveField(key);
                }
                doc.RemoveField(key);
            }

            doc.KeyName = "SubID";

            return doc;
        }

        /// <summary>
        ///  Returns the documentation for the object with this ID and SubID combination. This isn't
        ///  really meant to be displayed in a DocumentationDialog, rather it can be used to look up
        ///  subid-specific values like "postype".
        ///
        ///  (TODO: cache, and make it so that not every object stores the subid values for everything;
        ///  might need some kind of new "BaseObject" class that only has ID, not SubID.)
        /// </summary>
        public Documentation GetSubIDDocumentation()
        {
            Documentation doc = IDConstantsMapping.GetDocumentationForValue(ID);

            if (doc == null)
                return null;

            foreach (string key in doc.Keys)
            {
                if (key.Length >= 6 && key.Substring(0, 6) == "subid_")
                {
                    string range = key.Substring(6);
                    if (Helper.GetIntListFromRange(range).Contains(SubID))
                    {
                        Documentation subidDoc = doc.GetSubDocumentation(key);
                        return subidDoc;
                    }
                }
            }

            return doc;
        }
    }
}
