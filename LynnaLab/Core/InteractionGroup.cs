using System;
using System.Collections.Generic;

namespace LynnaLab
{

    public class InteractionGroup : ProjectDataType
    {
        List<InteractionData> interactionDataList = new List<InteractionData>();

        public InteractionGroup(Project p, String id) : base(p, id)
        {
            FileParser parser = Project.GetFileWithLabel(Identifier);
            InteractionData data = parser.GetData(Identifier) as InteractionData;

            while (data.GetInteractionType() != InteractionType.End && data.GetInteractionType() != InteractionType.EndPointer) {
                interactionDataList.Add(data);
                data = data.Next as InteractionData;
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
    }
}
