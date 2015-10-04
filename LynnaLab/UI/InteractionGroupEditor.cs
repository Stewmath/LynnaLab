using System;
using System.Collections.Generic;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public partial class InteractionGroupEditor : Gtk.Bin
    {
        public static String[] InteractionNames = {
            "Type 0 Interaction",
            "No Value Scriptable Interaction",
            "2-Value Scriptable Interaction",
            "Interaction Pointer",
            "Boss Interaction Pointer",
            "Conditional Interaction Pointer",
            "Random Position Enemy",
            "Specific Position Enemy",
            "Part",
            "4-Value Interaction",
            "Item Drop",
        };

        Project Project {
            get {
                if (interactionGroup == null) return null;
                return interactionGroup.Project;
            }
        }

        InteractionGroup interactionGroup;
        InteractionData activeData;

        Gtk.Frame pointerFrame;

        public InteractionGroupEditor()
        {
            this.Build();

            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                int i = indexSpinButton.ValueAsInt;
                if (interactionGroup == null || i == -1)
                    SetInteractionData(null);
                else
                    SetInteractionData(interactionGroup.GetInteractionData(i));
            };
        }

        public void SetInteractionGroup(InteractionGroup group) {
            this.interactionGroup = group;
            UpdateBoundaries();
            indexSpinButton.Value = 0;
            if (interactionGroup != null && interactionGroup.GetNumInteractions() != 0)
                SetInteractionData(interactionGroup.GetInteractionData(0));
            else {
                indexSpinButton.Value = -1;
                SetInteractionData(null);
            }
        }

        void SetInteractionDataIndex(int i) {
            if (interactionGroup == null || i < 0 || i >= interactionGroup.GetNumInteractions())
                SetInteractionData(null);
            else
                SetInteractionData(interactionGroup.GetInteractionData(i));
        }
        void SetInteractionData(InteractionData data) {
            activeData = data;

            foreach (Gtk.Widget widget in interactionDataContainer.Children) {
                interactionDataContainer.Remove(widget);
                widget.Destroy();
            }

            if (data == null) {
                frameLabel.Text = "";
                return;
            }
            frameLabel.Text = InteractionNames[(int)activeData.GetInteractionType()];

            ValueReferenceEditor eddie = new ValueReferenceEditor(Project,data.GetValueReferences());

            interactionDataContainer.Add(eddie);
            interactionDataContainer.ShowAll();
        }

        void UpdateBoundaries() {
            indexSpinButton.Adjustment.Lower = -1;
            int max=0;
            if (interactionGroup == null)
                max = -1;
            else
                max = interactionGroup.GetNumInteractions()-1;

            indexSpinButton.Adjustment.Upper = max;
            if (indexSpinButton.ValueAsInt > max) {
                indexSpinButton.Value = max;
            }

            SetInteractionDataIndex(indexSpinButton.ValueAsInt);
        }

        protected void OnDeleteButtonClicked(object sender, EventArgs e)
        {
            if (interactionGroup != null && indexSpinButton.ValueAsInt != -1) {
                interactionGroup.RemoveInteraction(indexSpinButton.ValueAsInt);
                UpdateBoundaries();
            }
        }

        protected void OnAddButtonClicked(object sender, EventArgs e)
        {
            if (interactionGroup == null) return;

            AddInteractionDialog d = new AddInteractionDialog();
            d.Run();
            if (d.InteractionTypeToAdd != InteractionType.End) {
                if (interactionGroup == null) return;

                interactionGroup.InsertInteraction(indexSpinButton.ValueAsInt+1, d.InteractionTypeToAdd);
                UpdateBoundaries();
                indexSpinButton.Value = indexSpinButton.ValueAsInt+1;
            }
        }
    }
}
