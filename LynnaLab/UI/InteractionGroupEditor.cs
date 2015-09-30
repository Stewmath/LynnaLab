using System;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class InteractionGroupEditor : Gtk.Bin
    {
        InteractionGroup interactionGroup;

        public InteractionGroupEditor()
        {
            this.Build();

            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
            };
        }

        public void SetInteractionGroup(InteractionGroup group) {
            indexSpinButton.Adjustment.Lower = -1;
            indexSpinButton.Adjustment.Upper = group.GetNumInteractions()-1;
            indexSpinButton.Value = -1;
            this.interactionGroup = group;
        }

        void SetInteractionData(InteractionData data) {
        }
    }
}

