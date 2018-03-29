
// This file has been generated by the GUI designer. Do not modify.
namespace LynnaLab
{
	public partial class AddObjectDialog
	{
		private global::Gtk.HBox hbox1;

		private global::Gtk.Alignment alignment2;

		private global::Gtk.SpinButton typeSpinButton;

		private global::Gtk.Label infoLabel;

		private global::Gtk.Label descriptionLabel;

		private global::Gtk.Button buttonCancel;

		private global::Gtk.Button buttonOk;

		protected virtual void Build()
		{
			global::Stetic.Gui.Initialize(this);
			// Widget LynnaLab.AddObjectDialog
			this.Name = "LynnaLab.AddObjectDialog";
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			// Internal child LynnaLab.AddObjectDialog.VBox
			global::Gtk.VBox w1 = this.VBox;
			w1.Name = "dialog1_VBox";
			w1.BorderWidth = ((uint)(2));
			// Container child dialog1_VBox.Gtk.Box+BoxChild
			this.hbox1 = new global::Gtk.HBox();
			this.hbox1.Name = "hbox1";
			this.hbox1.Spacing = 6;
			// Container child hbox1.Gtk.Box+BoxChild
			this.alignment2 = new global::Gtk.Alignment(0F, 0F, 0F, 0F);
			this.alignment2.Name = "alignment2";
			// Container child alignment2.Gtk.Container+ContainerChild
			this.typeSpinButton = new global::Gtk.SpinButton(0D, 10D, 1D);
			this.typeSpinButton.CanFocus = true;
			this.typeSpinButton.Name = "typeSpinButton";
			this.typeSpinButton.Adjustment.PageIncrement = 10D;
			this.typeSpinButton.ClimbRate = 1D;
			this.typeSpinButton.Numeric = true;
			this.alignment2.Add(this.typeSpinButton);
			this.hbox1.Add(this.alignment2);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.hbox1[this.alignment2]));
			w3.Position = 0;
			w3.Expand = false;
			w3.Fill = false;
			// Container child hbox1.Gtk.Box+BoxChild
			this.infoLabel = new global::Gtk.Label();
			this.infoLabel.Name = "infoLabel";
			this.infoLabel.Xalign = 0F;
			this.infoLabel.LabelProp = global::Mono.Unix.Catalog.GetString("Info Label");
			this.hbox1.Add(this.infoLabel);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.hbox1[this.infoLabel]));
			w4.Position = 1;
			w4.Expand = false;
			w4.Fill = false;
			w1.Add(this.hbox1);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(w1[this.hbox1]));
			w5.Position = 0;
			w5.Expand = false;
			w5.Fill = false;
			// Container child dialog1_VBox.Gtk.Box+BoxChild
			this.descriptionLabel = new global::Gtk.Label();
			this.descriptionLabel.Name = "descriptionLabel";
			this.descriptionLabel.Xalign = 0.2F;
			this.descriptionLabel.Yalign = 0.1F;
			this.descriptionLabel.LabelProp = global::Mono.Unix.Catalog.GetString("Description Label");
			this.descriptionLabel.Wrap = true;
			w1.Add(this.descriptionLabel);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(w1[this.descriptionLabel]));
			w6.Position = 1;
			// Internal child LynnaLab.AddObjectDialog.ActionArea
			global::Gtk.HButtonBox w7 = this.ActionArea;
			w7.Name = "dialog1_ActionArea";
			w7.Spacing = 10;
			w7.BorderWidth = ((uint)(5));
			w7.LayoutStyle = ((global::Gtk.ButtonBoxStyle)(4));
			// Container child dialog1_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.buttonCancel = new global::Gtk.Button();
			this.buttonCancel.CanDefault = true;
			this.buttonCancel.CanFocus = true;
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.UseStock = true;
			this.buttonCancel.UseUnderline = true;
			this.buttonCancel.Label = "gtk-cancel";
			this.AddActionWidget(this.buttonCancel, -6);
			global::Gtk.ButtonBox.ButtonBoxChild w8 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w7[this.buttonCancel]));
			w8.Expand = false;
			w8.Fill = false;
			// Container child dialog1_ActionArea.Gtk.ButtonBox+ButtonBoxChild
			this.buttonOk = new global::Gtk.Button();
			this.buttonOk.CanDefault = true;
			this.buttonOk.CanFocus = true;
			this.buttonOk.Name = "buttonOk";
			this.buttonOk.UseStock = true;
			this.buttonOk.UseUnderline = true;
			this.buttonOk.Label = "gtk-ok";
			this.AddActionWidget(this.buttonOk, -5);
			global::Gtk.ButtonBox.ButtonBoxChild w9 = ((global::Gtk.ButtonBox.ButtonBoxChild)(w7[this.buttonOk]));
			w9.Position = 1;
			w9.Expand = false;
			w9.Fill = false;
			if ((this.Child != null))
			{
				this.Child.ShowAll();
			}
			this.DefaultWidth = 405;
			this.DefaultHeight = 208;
			this.Show();
			this.typeSpinButton.ValueChanged += new global::System.EventHandler(this.OnTypeSpinButtonValueChanged);
			this.buttonCancel.Clicked += new global::System.EventHandler(this.OnButtonCancelClicked);
			this.buttonOk.Clicked += new global::System.EventHandler(this.OnButtonOkClicked);
		}
	}
}
