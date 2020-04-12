using System;
using System.Collections.Generic;
using Bitmap = System.Drawing.Bitmap;

namespace LynnaLab {

    // A "box" allowing one to select items in it, drag them to change ordering, and right-click to
    // bring up a menu.
    public abstract class SelectionBox : TileGridViewer {

        // Constructors

        public SelectionBox() : base() {
            base.Width = 8;
            base.Height = 2;
            base.TileWidth = 18;
            base.TileHeight = 18;
            base.TilePaddingX = 2;
            base.TilePaddingY = 2;

            base.Selectable = true;

            TileGridEventHandler dragCallback = (sender, index) => {
                if (index != SelectedIndex) {
                    OnMoveSelection(SelectedIndex, index);
                    SelectedIndex = index;
                }
            };

            base.AddMouseAction(MouseButton.LeftClick, MouseModifier.Any | MouseModifier.Drag,
                    GridAction.Callback, dragCallback);

            Gtk.ButtonPressEventHandler OnButtonPressEvent = delegate(object sender, Gtk.ButtonPressEventArgs args) {
                int x,y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);

                if (state.HasFlag(Gdk.ModifierType.Button3Mask)) {
                    if (HoveringIndex != -1)
                        SelectedIndex = HoveringIndex;

                    ShowPopupMenu(args.Event);
                }
            };

            this.ButtonPressEvent += new Gtk.ButtonPressEventHandler(OnButtonPressEvent);
        }


        // Public methods

        public void SetSelectedIndex(int index) {
            SelectedIndex = index;
        }


        // Protected methods

        protected abstract void OnMoveSelection(int oldIndex, int newIndex);
        protected abstract void ShowPopupMenu(Gdk.EventButton ev);

        protected override void TileDrawer(int index, Cairo.Context cr) {
            if (index > MaxIndex)
                return;

            // Draw index number with pango
            using (var context = Pango.CairoHelper.CreateContext(cr))
            using (var layout = new Pango.Layout(context)) {
                cr.SetSourceColor(new Cairo.Color(0, 0, 0));

                layout.Width = Pango.Units.FromPixels(TileWidth);
                layout.Height = Pango.Units.FromPixels(TileHeight);
                layout.Alignment = Pango.Alignment.Center;

                // TODO: install the font on the system
                layout.FontDescription = Pango.FontDescription.FromString("ZeldaOracles 12");

                layout.SetText(index.ToString("X"));
                Pango.CairoHelper.ShowLayout(cr, layout);
            }
        }
    }
}

