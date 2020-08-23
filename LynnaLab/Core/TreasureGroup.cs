using System;
using System.Collections.Generic;
using Util;

namespace LynnaLab
{
    /// Represents an "INTERACID_TREASURE" value (1 byte). This can be used to lookup
    /// a "TreasureObject" which has an additional subID.
    public class TreasureGroup : ProjectIndexedDataType {
        TreasureObject[] treasureObjectCache = new TreasureObject[256];
        Data dataStart;


        internal TreasureGroup(Project p, int index) : base(p, index) {
            if (Index >= Project.NumTreasures)
                throw new InvalidTreasureException(
                        string.Format("Treasure {0:X2} doesn't exist!", Index));

            DetermineDataStart();
        }

        // Properties

        public int NumTreasureObjectSubids {
            get {
                Data data = dataStart;
                if (!UsesPointer)
                    return 1;
                try {
                    data = Project.GetData(data.NextData.GetValue(0));
                }
                catch (InvalidLookupException) {
                    return 0;
                }
                return TraverseSubidData(ref data, 256);
            }
        }

        bool UsesPointer {
            get { return (dataStart.GetIntValue(0) & 0x80) != 0; }
        }


        // Methods

        public TreasureObject GetTreasureObject(int subid) {
            if (treasureObjectCache[subid] == null) {
                Data data = GetSubidBaseData(subid);
                if (data == null)
                    return null;
                treasureObjectCache[subid] = new TreasureObject(this, subid, data);
            }
            return treasureObjectCache[subid];
        }

        public TreasureObject AddTreasureObjectSubid() {
            if (NumTreasureObjectSubids >= 256)
                return null;

            if (NumTreasureObjectSubids == 0) {
                // This should only happen when the treasure has "using a pointer" marked, but is
                // not using a pointer. So just unset that bit.
                dataStart.SetByteValue(0, 0);
                return GetTreasureObject(0);
            }

            if (!UsesPointer) {
                // We need to create a pointer for the subid list and move the old data to the start
                // of the list. Be careful to ensure that the old data objects are moved, and not
                // deleted, so that we don't break the TreasureObject's that were built on them.
                var componentList = new List<FileComponent>();
                Data data = dataStart;
                if (TraverseSubidData(ref data, 1, (c) => componentList.Add(c)) != 1)
                    return null;

                // Create pointer
                FileParser parser = dataStart.FileParser;
                string labelName = Project.GetUniqueLabelName(
                        string.Format("treasureObjectData{0:x2}", Index));
                parser.InsertParseableTextBefore(dataStart, new string[] {
                    "\t.db $80",
                    "\t.dw " + labelName,
                    "\t.db $00"
                });

                // Detach old data
                foreach (var c in componentList)
                    c.Detach();

                // Create label
                parser.InsertParseableTextAfter(null, new string[] { labelName + ":" });

                // Move old data to after the label
                foreach (var c in componentList)
                    parser.InsertComponentAfter(null, c);

                // Update dataStart (since the old data was moved)
                DetermineDataStart();
            }


            // Pointer either existed already or was just created. Insert new subid's data.
            Data lastSubidData = Project.GetData(dataStart.NextData.GetValue(0));
            TraverseSubidData(ref lastSubidData, NumTreasureObjectSubids - 1);
            lastSubidData = lastSubidData.NextData.NextData.NextData;

            dataStart.FileParser.InsertParseableTextAfter(lastSubidData,
                    new string[] { "\t.db $00 $00 $00 $00" });
            return GetTreasureObject(NumTreasureObjectSubids - 1);
        }


        Data GetSubidBaseData(int subid) {
            Data data = dataStart;

            if (!UsesPointer) {
                if (subid == 0)
                    return data;
                else
                    return null;
            }

            // Uses pointer

            try {
                data = Project.GetData(data.NextData.GetValue(0)); // Follow pointer
            }
            catch (InvalidLookupException) {
                // Sometimes there is no pointer even when the "pointer" bit is set.
                return null;
            }
            if (TraverseSubidData(ref data, subid) != subid)
                return null;

            return data;
        }

        /// Traverses subid data up to "subid", returns the total number of subids actually
        /// traversed (there may be less than requested).
        /// Labels are considered to end a sequence of subid data.
        int TraverseSubidData(ref Data data, int subid, Action<FileComponent> action = null) {
            int count = 0;
            while (count < subid) {
                FileComponent com = data;
                for (int j=0; j<4;) {
                    if (action != null)
                        action(com);
                    if (com is Data) {
                        if ((com as Data).Size == -1)
                            return count;
                        j += (com as Data).Size;
                        if (j > 4)
                            return count;
                    }
                    else if (com is Label || com == null)
                        return count;
                    com = com.Next;
                }
                data = com as Data;
                count++;
            }
            return count;
        }

        void DetermineDataStart() {
            dataStart = Project.GetData("treasureObjectData", Index*4);
        }
    }
}
