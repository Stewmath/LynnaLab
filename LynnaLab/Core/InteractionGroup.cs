using System;
using System.Collections.Generic;

namespace LynnaLab
{

    public class InteractionGroup : ProjectIndexedDataType
    {
        List<InteractionData> interactionDataList = new List<InteractionData>();

        public InteractionGroup(Project p, int index) : base(p, index)
        {
            FileParser parser = Project.GetFileWithLabel(GetDataLabel());

            InteractionData data = parser.GetData(GetDataLabel()) as InteractionData;
            while (data.Command != "interacend") {
                interactionDataList.Add(data);
            }
        }

        public InteractionData GetInteractionData(int index) {
            return interactionDataList[index];
        }
        public int GetNumInteractions() {
            return interactionDataList.Count;
        }

        String GetDataLabel() {
            return "group" + (Index/0x100).ToString("x") + "Map" + (Index%0x100).ToString("x2") + "InteractionData";
        }
    }
}
