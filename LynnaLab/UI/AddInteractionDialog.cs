using System;

namespace LynnaLab
{
    public partial class AddInteractionDialog : Gtk.Dialog
    {
        InteractionType interactionType;

        public InteractionType InteractionTypeToAdd {
            get { return interactionType; }
        }

        public AddInteractionDialog()
        {
            this.Build();
            UpdateLabel();
        }

        public void UpdateLabel() {
            interactionType = (InteractionType)typeSpinButton.ValueAsInt;
            infoLabel.Text = InteractionGroupEditor.InteractionNames[(int)interactionType];
        }

        protected void OnTypeSpinButtonValueChanged(object sender, EventArgs e)
        {
            UpdateLabel();
        }

        protected void OnButtonCancelClicked(object sender, EventArgs e)
        {
            interactionType = InteractionType.End;
            this.Destroy();
        }

        protected void OnButtonOkClicked(object sender, EventArgs e)
        {
            this.Destroy();
        }
    }
}

