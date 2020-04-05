using System;
using System.Collections.Generic;
using System.Drawing;

namespace LynnaLab {
    // Public interface over the ObjectData class.
    // As it turned out, I ended up putting a lot of the abstraction in the "ObjectData" class,
    // when it should have gone here in the end, making the whole thing more complicated. Oh well...
    public class ObjectDefinition : ValueReferenceGroup
    {
        ObjectGroup objectGroup;
        ObjectData objectData;


        // Constructors

        public ObjectDefinition(ObjectGroup group, ObjectData objectData) {
            this.objectGroup = group;
            this.objectData = objectData;

            var valueReferences = new List<ValueReference>();
            var handler = new ObjectValueReferenceHandler(this);

            foreach (var vref in objectData.GetValueReferences()) {
                var newVref = new AbstractIntValueReference(vref, handler);
                valueReferences.Add(newVref);
            }

            base.SetValueReferences(valueReferences);
        }


        // Public methods

        public ObjectType GetObjectType() {
            return objectData.GetObjectType();
        }

        /// <summary>
        ///  Returns true if the X/Y variables are 4-bits instead of 8 (assuming it has X/Y in the
        ///  first place).
        /// </summary>
        public bool HasShortenedXY() {
            return IsTypeWithShortenedXY() || GetSubIDDocumentation()?.GetField("postype") == "short";
        }

        public bool HasXY() {
            return HasValue("X") && HasValue("Y");
        }

        // Return the center x-coordinate of the object.
        // This is different from 'GetIntValue("X")' because sometimes objects store both their Y and
        // X values in one byte. This will take care of that, and will multiply the value when the
        // positions are in this short format (ie. range $0-$f becomes $08-$f8).
        public byte GetX() {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                int n = GetIntValue("Y")&0xf;
                return (byte)(n*16+8);
            }
            else if (IsTypeWithShortenedXY()) {
                int n = GetIntValue("X");
                return (byte)(n*16+8);
            }
            else
                return (byte)GetIntValue("X");
        }
        // Return the center y-coordinate of the object
        public byte GetY() {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                int n = GetIntValue("Y")>>4;
                return (byte)(n*16+8);
            }
            else if (IsTypeWithShortenedXY()) {
                int n = GetIntValue("Y");
                return (byte)(n*16+8);
            }
            else
                return (byte)GetIntValue("Y");
        }

        public void SetX(byte n) {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                byte y = (byte)(GetIntValue("Y")&0xf0);
                y |= (byte)(n/16);
                SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                SetValue("X", n/16);
            else
                SetValue("X", n);
        }
        public void SetY(byte n) {
            if (GetSubIDDocumentation()?.GetField("postype") == "short") {
                byte y = (byte)(GetIntValue("Y")&0x0f);
                y |= (byte)(n&0xf0);
                SetValue("Y", y);
            }
            else if (IsTypeWithShortenedXY())
                SetValue("Y", n/16);
            else
                SetValue("Y", n);
        }

        public GameObject GetGameObject() {
            if (GetObjectType() == ObjectType.Interaction) {
                return Project.GetIndexedDataType<InteractionObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            else if (GetObjectType() == ObjectType.RandomEnemy || GetObjectType() == ObjectType.SpecificEnemyA
                    || GetObjectType() == ObjectType.SpecificEnemyB) {
                return Project.GetIndexedDataType<EnemyObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            else if (GetObjectType() == ObjectType.Part) {
                return Project.GetIndexedDataType<PartObject>((GetIntValue("ID")<<8) | GetIntValue("SubID"));
            }
            // TODO: other types
            return null;
        }

        public Documentation GetIDDocumentation() {
            return GetGameObject()?.GetIDDocumentation();
        }

        public Documentation GetSubIDDocumentation() {
            return GetGameObject()?.GetSubIDDocumentation();
        }


        internal void SetObjectData(ObjectData data) {
            objectData = data;
        }


        // Private methods

        // Returns true if the object's type causes the XY values to have 4 bits rather than 8.
        // (DOES NOT account for "@postype" parameter which can set interactions to have both Y/X
        // positions stored in the Y variable.)
        bool IsTypeWithShortenedXY() {
            // Don't include "Part" objects because, when converted to the "QuadrupleValue" type,
            // they can have fine-grained position values.
            return GetObjectType() == ObjectType.ItemDrop;
        }


        class ObjectValueReferenceHandler : BasicIntValueReferenceHandler {
            ObjectDefinition parent;


            public override Project Project { get { return parent.objectData.Project; } }


            public ObjectValueReferenceHandler(ObjectDefinition parent) {
                this.parent = parent;
            }


            public override int GetIntValue(string name) {
                return parent.objectData.GetIntValue(name);
            }

            public override void SetValue(string name, int value) {
                if (value == GetIntValue(name))
                    return;
                parent.objectGroup.Isolate();
                parent.objectData.SetValue(name, value);
            }

            public override void AddValueModifiedHandler(EventHandler handler) {
                parent.objectData.AddValueModifiedHandler(handler);
            }
            public override void RemoveValueModifiedHandler(EventHandler handler) {
                parent.objectData.RemoveValueModifiedHandler(handler);
            }
        }
    }
}
