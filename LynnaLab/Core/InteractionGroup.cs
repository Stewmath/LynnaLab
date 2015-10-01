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
        }

        public InteractionData GetInteractionData(int index) {
            return interactionDataList[index];
        }
        public int GetNumInteractions() {
            return interactionDataList.Count;
        }
    }
}
