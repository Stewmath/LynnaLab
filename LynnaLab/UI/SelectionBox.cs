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
            base.BackgroundColor = new Cairo.Color(0.8, 0.8, 0.8);

            TileGridEventHandler dragCallback = (sender, index) => {
                if (index != SelectedIndex) {
                    OnMoveSelection(SelectedIndex, index);
                    SelectedIndex = index;
                }
            };

            base.AddMouseAction(MouseButton.LeftClick, MouseModifier.Any | MouseModifier.Drag,
                    GridAction.Callback, dragCallback);


            this.ButtonPressEvent += (sender, args) => {
                if (args.Event.Button == 3) {
                    if (HoveringIndex != -1)
                        SelectedIndex = HoveringIndex;
                    ShowPopupMenu(args.Event);
                }
            };
        }


        // Public methods

        public void SetSelectedIndex(int index) {
            SelectedIndex = index;
        }


        // Protected methods

        protected abstract void OnMoveSelection(int oldIndex, int newIndex);
        protected abstract void ShowPopupMenu(Gdk.EventButton ev);
    }
}

