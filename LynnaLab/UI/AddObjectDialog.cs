using System;

using LynnaLib;

namespace LynnaLab
{
    public class AddObjectDialog : Gtk.Dialog
    {
        Gtk.ComboBoxText comboBox;
        Gtk.Label descriptionLabel;

        ObjectType objectType = ObjectType.End;

        public ObjectType ObjectTypeToAdd
        {
            get { return objectType; }
        }

        public AddObjectDialog()
        {
            comboBox = new Gtk.ComboBoxText();
            descriptionLabel = new Gtk.Label();

            ContentArea.Add(comboBox);
            ContentArea.Add(descriptionLabel);
            AddButton("Add", Gtk.ResponseType.Ok);

            foreach (string s in ObjectGroupEditor.ObjectNames)
            {
                comboBox.AppendText(s);
            }
            comboBox.Active = 0;
            comboBox.Changed += OnComboBoxChanged;

            UpdateLabel();

            this.ShowAll();
        }

        public void UpdateLabel()
        {
            objectType = (ObjectType)comboBox.Active;
            //infoLabel.Text = ObjectGroupEditor.ObjectNames[(int)objectType];
            descriptionLabel.Text = ObjectGroupEditor.ObjectDescriptions[(int)objectType];
        }

        protected void OnComboBoxChanged(object sender, EventArgs e)
        {
            UpdateLabel();
        }

        protected void OnButtonCancelClicked(object sender, EventArgs e)
        {
            objectType = ObjectType.End;
            this.Dispose();
        }

        protected void OnButtonOkClicked(object sender, EventArgs e)
        {
            this.Dispose();
        }
    }
}

