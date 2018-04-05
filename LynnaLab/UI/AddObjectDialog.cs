using System;

namespace LynnaLab
{
    public partial class AddObjectDialog : Gtk.Dialog
    {
        ObjectType objectType;

        public ObjectType ObjectTypeToAdd {
            get { return objectType; }
        }

        public AddObjectDialog()
        {
            this.Build();
            //this.Add(w1);
            UpdateLabel();
        }

        public void UpdateLabel() {
            objectType = (ObjectType)typeSpinButton.ValueAsInt;
            infoLabel.Text = ObjectGroupEditor.ObjectNames[(int)objectType];
            descriptionLabel.Text = ObjectGroupEditor.ObjectDescriptions[(int)objectType];
        }

        protected void OnTypeSpinButtonValueChanged(object sender, EventArgs e)
        {
            UpdateLabel();
        }

        protected void OnButtonCancelClicked(object sender, EventArgs e)
        {
            objectType = ObjectType.End;
            this.Destroy();
        }

        protected void OnButtonOkClicked(object sender, EventArgs e)
        {
            this.Destroy();
        }
    }
}

