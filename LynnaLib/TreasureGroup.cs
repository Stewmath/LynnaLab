using System;
using System.Collections.Generic;
using System.Linq;
using Util;

namespace LynnaLib
{
    /// Represents an "INTERACID_TREASURE" value (1 byte). This can be used to lookup
    /// a "TreasureObject" which has an additional subID.
    public class TreasureGroup : ProjectIndexedDataType
    {
        TreasureObject[] treasureObjectCache = new TreasureObject[256];

        // TODO: Update with undo/redo
        Data dataStart;


        internal TreasureGroup(Project p, int index) : base(p, index)
        {
            if (Index >= Project.NumTreasures)
                throw new InvalidTreasureException(
                        string.Format("Treasure {0:X2} doesn't exist!", Index));

            DetermineDataStart();
        }

        // Properties

        public int NumTreasureObjectSubids
        {
            get
            {
                Data data = dataStart;
                if (!UsesPointer)
                    return 1;
                try
                {
                    data = Project.GetData(data.GetValue(0));
                }
                catch (InvalidLookupException)
                {
                    return 0;
                }
                return TraverseSubidData(ref data, 256);
            }
        }

        bool UsesPointer
        {
            get { return dataStart.CommandLowerCase == "m_treasurepointer"; }
        }


        // Methods

        public TreasureObject GetTreasureObject(int subid)
        {
            if (treasureObjectCache[subid] == null)
            {
                Data data = GetSubidBaseData(subid);
                if (data == null)
                    return null;
                treasureObjectCache[subid] = new TreasureObject(this, subid, data);
            }
            return treasureObjectCache[subid];
        }

        public TreasureObject AddTreasureObjectSubid()
        {
            if (NumTreasureObjectSubids >= 256)
                return null;

            Func<int, bool, TreasureObject, string> ConstructTreasureSubidString
                = (subid, inSubidTable, lastTreasureObject) =>
            {
                byte lastGfx = 0;
                string lastText = "$ff";
                if (lastTreasureObject != null)
                {
                    lastGfx = (byte)lastTreasureObject.Graphics;
                    lastText = lastTreasureObject.ValueReferenceGroup.GetValue("Text Index");
                }
                string prefix = string.Format("/* ${0:x2} */ ", Index);
                string body = string.Format("$38, $00, {0}, ${1:x2}, TREASURE_OBJECT_{2}_{3:x2}",
                        lastText,
                        lastGfx,
                        Project.TreasureMapping.ByteToString(Index).Substring(9),
                        subid);
                if (inSubidTable)
                    return "\tm_TreasureSubid " + body;
                else
                    return "\t" + prefix + "m_TreasureSubid   " + body;
            };

            if (NumTreasureObjectSubids == 0)
            {
                // This should only happen when the treasure is using "m_treasurepointer", but has
                // a null pointer. So rewrite that line with a blank treasure.
                dataStart.FileParser.InsertParseableTextAfter(dataStart, new string[] {
                    ConstructTreasureSubidString(0, false, null)
                });
                dataStart.Detach();

                // Update dataStart (since the old data was deleted)
                DetermineDataStart();

                return GetTreasureObject(0);
            }

            // Otherwise, a previous subid existed, so let's copy over some parameters from it
            TreasureObject lastSubid = GetTreasureObject(NumTreasureObjectSubids - 1);

            if (!UsesPointer)
            {
                // We need to create a pointer for the subid list and move the old data to the start
                // of the list. Be careful to ensure that the old data objects are moved, and not
                // deleted, so that we don't break the TreasureObject's that were built on them.
                // Create pointer
                FileParser parser = dataStart.FileParser;
                string labelName = Project.GetUniqueLabelName(
                        string.Format("treasureObjectData{0:x2}", Index));
                parser.InsertParseableTextBefore(dataStart, new string[] { string.Format(
                    "\t/* ${0:x2} */ m_TreasurePointer {1}", Index, labelName)
                });

                dataStart.Detach();

                // We want to insert the data at the end of the file, but it must be within the
                // section, so we need to check for the ".ends" directive and put it above there.
                var sectionEnd = parser.FileStructure.Where((x) => x.GetString().Trim().ToLower() == ".ends");
                FileComponent insertPos = (sectionEnd.Count() == 0 ? null : sectionEnd.Last().Prev);

                // Create label
                insertPos = parser.InsertParseableTextAfter(insertPos, new string[] { labelName + ":" });

                // Create "m_BeginTreasureSubids" macro
                insertPos = parser.InsertParseableTextAfter(insertPos, new string[] { string.Format(
                            "\tm_BeginTreasureSubids " + Project.TreasureMapping.ByteToString(Index))
                });

                // Move old data to after the label
                insertPos = parser.InsertComponentAfter(insertPos, dataStart);

                // Adjust spacing since it's a bit different in the subid table
                dataStart.SetSpacing(0, "\t");
                dataStart.SetSpacing(1, "");

                // Insert newline after the new subid table
                insertPos = parser.InsertParseableTextAfter(insertPos, new string[] { "" });

                // Update dataStart (since the old data was moved)
                DetermineDataStart();
            }


            // Pointer either existed already or was just created. Insert new subid's data.
            Data lastSubidData = Project.GetData(dataStart.GetValue(0));
            TraverseSubidData(ref lastSubidData, NumTreasureObjectSubids - 1);

            dataStart.FileParser.InsertParseableTextAfter(lastSubidData,
                    new string[] { ConstructTreasureSubidString(NumTreasureObjectSubids, true, lastSubid) });
            return GetTreasureObject(NumTreasureObjectSubids - 1);
        }


        Data GetSubidBaseData(int subid)
        {
            Data data = dataStart;

            if (!UsesPointer)
            {
                if (subid == 0)
                    return data;
                else
                    return null;
            }

            // Uses pointer

            try
            {
                data = Project.GetData(data.GetValue(0)); // Follow pointer
            }
            catch (InvalidLookupException)
            {
                // Sometimes there is no pointer even when the "pointer" bit is set.
                return null;
            }
            if (TraverseSubidData(ref data, subid) != subid + 1)
                return null;

            return data;
        }

        /// Traverses subid data up to "subid", returns the total number of subids actually
        /// traversed (there may be less than requested).
        /// Labels are considered to end a sequence of subid data.
        int TraverseSubidData(ref Data data, int subid, Action<FileComponent> action = null)
        {
            int count = 0;
            FileComponent com = data;
            while (count <= subid)
            {
                while (!(com is Data))
                {
                    if (com is Label || com == null)
                        return count;
                    com = com.Next;
                }
                if (action != null)
                    action(com);
                count++;
                data = com as Data;
                com = com.Next;
            }
            return count;
        }

        void DetermineDataStart()
        {
            dataStart = Project.GetData("treasureObjectData", Index * 4);
        }
    }
}
