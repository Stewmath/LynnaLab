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

        InteractionGroup _interactionGroup;
        InteractionData activeData;

        Gtk.Frame pointerFrame;

        Project Project {
            get {
                if (InteractionGroup == null) return null;
                return InteractionGroup.Project;
            }
        }
        public InteractionGroup InteractionGroup {
            get { return _interactionGroup; }
        }
        

        public InteractionGroupEditor()
        {
            this.Build();

            indexSpinButton.ValueChanged += delegate(object sender, EventArgs e) {
                int i = indexSpinButton.ValueAsInt;
                if (InteractionGroup == null || i == -1)
                    SetInteractionData(null);
                else
                    SetInteractionData(InteractionGroup.GetInteractionData(i));
            };
        }

        public void SetInteractionGroup(InteractionGroup group) {
            _interactionGroup = group;
            UpdateBoundaries();
            indexSpinButton.Value = 0;
            if (InteractionGroup != null && InteractionGroup.GetNumInteractions() != 0)
                SetInteractionData(InteractionGroup.GetInteractionData(0));
            else {
                indexSpinButton.Value = -1;
                SetInteractionData(null);
            }
        }

        void SetInteractionDataIndex(int i) {
            if (InteractionGroup == null || i < 0 || i >= InteractionGroup.GetNumInteractions())
                SetInteractionData(null);
            else
                SetInteractionData(InteractionGroup.GetInteractionData(i));
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
            if (InteractionGroup == null)
                max = -1;
            else
                max = InteractionGroup.GetNumInteractions()-1;

            indexSpinButton.Adjustment.Upper = max;
            if (indexSpinButton.ValueAsInt > max) {
                indexSpinButton.Value = max;
            }

            SetInteractionDataIndex(indexSpinButton.ValueAsInt);
        }

        protected void OnDeleteButtonClicked(object sender, EventArgs e)
        {
            if (InteractionGroup != null && indexSpinButton.ValueAsInt != -1) {
                InteractionGroup.RemoveInteraction(indexSpinButton.ValueAsInt);
                UpdateBoundaries();
            }
        }

        protected void OnAddButtonClicked(object sender, EventArgs e)
        {
            if (InteractionGroup == null) return;

            AddInteractionDialog d = new AddInteractionDialog();
            d.Run();
            if (d.InteractionTypeToAdd != InteractionType.End) {
                if (InteractionGroup == null) return;

                InteractionGroup.InsertInteraction(indexSpinButton.ValueAsInt+1, d.InteractionTypeToAdd);
                UpdateBoundaries();
                indexSpinButton.Value = indexSpinButton.ValueAsInt+1;
            }
        }
    }
}
