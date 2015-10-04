using System;
using System.Collections.Generic;

namespace LynnaLab
{

    public class InteractionGroup : ProjectDataType
    {
        public static string[] InteractionCommands = {
            "Interac0",
            "NoValue",
            "DoubleValue",
            "Pointer",
            "BossPointer",
            "Conditional",
            "RandomEnemy",
            "SpecificEnemy",
            "Part",
            "QuadrupleValue",
            "InteracA",
            "InteracEnd",
            "InteracEndPointer"
        };


        List<InteractionData> interactionDataList = new List<InteractionData>();
        FileParser parser;


        public InteractionGroup(Project p, String id) : base(p, id)
        {
            parser = Project.GetFileWithLabel(Identifier);
            InteractionData data = parser.GetData(Identifier) as InteractionData;

            while (data.GetInteractionType() != InteractionType.End && data.GetInteractionType() != InteractionType.EndPointer) {
                interactionDataList.Add(data);
                data = data.NextData as InteractionData;
            }
            interactionDataList.Add(data);
        }

        public InteractionData GetInteractionData(int index) {
            return interactionDataList[index];
        }
        public int GetNumInteractions() {
            return interactionDataList.Count-1; // Not counting InteracEnd
        }

        public void RemoveInteraction(int index) {
            if (index >= interactionDataList.Count-1)
                throw new Exception("Array index out of bounds.");

            InteractionData data = interactionDataList[index];
            data.Detach();
            interactionDataList.RemoveAt(index);
        }

        public void InsertInteraction(int index, InteractionType type) {
            IList<string> values = ValueReference.GetDefaultValues(InteractionData.interactionValueReferences[(int)type]);
            if (type >= InteractionType.Pointer && type <= InteractionType.Conditional)
                values[0] = "interactionData4000"; // Compileable default pointer

            InteractionData data = new InteractionData(Project, InteractionCommands[(int)type], values, parser,
                    new int[]{-1}, // Tab at start of line
                    type);
            data.InsertIntoParserBefore(interactionDataList[index]);
            interactionDataList.Insert(index, data);
        }

        // Returns true if no data is shared with another label
        bool IsIsolated() {
            return true;
        }
    }
}
