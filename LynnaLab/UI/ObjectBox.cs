using System;
using System.Collections.Generic;
using Bitmap = System.Drawing.Bitmap;

namespace LynnaLab {

    public class ObjectBox : SelectionBox {
        public ObjectGroup ObjectGroup { get; private set; }


        // Constructors

        public ObjectBox(ObjectGroup group) : base() {
            this.ObjectGroup = group;

            base.MaxIndex = ObjectGroup.GetNumObjects() - 1;

            ObjectGroup.AddModifiedHandler(OnObjectGroupModified);
        }


        protected override void OnDestroyed() {
            ObjectGroup.RemoveModifiedHandler(OnObjectGroupModified);
            base.OnDestroyed();
        }


        void OnObjectGroupModified(object sender, EventArgs args) {
            MaxIndex = ObjectGroup.GetNumObjects() - 1;
            QueueDraw();
        }


        // SelectionBox overrides

        protected override void OnMoveSelection(int oldIndex, int newIndex) {
            ObjectGroup.MoveObject(oldIndex, newIndex);
        }

        protected override void ShowPopupMenu(Gdk.Event ev) {
            Gtk.Menu menu = new Gtk.Menu();
            for (int i=0; i<ObjectGroupEditor.ObjectNames.Length; i++) {
                if (i >= 2 && i <= 4) // Skip "Pointer" objects
                    continue;
                Gtk.MenuItem item = new Gtk.MenuItem("Add " + ObjectGroupEditor.ObjectNames[i]);
                menu.Append(item);

                int index = i;

                item.Activated += (sender, args) => {
                    SetSelectedIndex(ObjectGroup.AddObject((ObjectType)index));
                };
            }

            if (HoveringIndex != -1) {
                menu.Append(new Gtk.SeparatorMenuItem());

                Gtk.MenuItem deleteItem = new Gtk.MenuItem("Delete");
                deleteItem.Activated += (sender, args) => {
                    if (SelectedIndex != -1)
                        ObjectGroup.RemoveObject(SelectedIndex);
                };
                menu.Append(deleteItem);
            }

            menu.AttachToWidget(this, null);
            menu.ShowAll();
            menu.PopupAtPointer(ev);
        }

        protected override void TileDrawer(int index, Cairo.Context cr) {
            if (index >= ObjectGroup.GetNumObjects())
                return;

            ObjectDefinition obj = ObjectGroup.GetObject(index);
            cr.SetSourceColor(ObjectGroupEditor.GetObjectColor(obj.GetObjectType()));
            cr.Rectangle(0, 0, 18, 18);
            cr.Fill();
            cr.Rectangle(1, 1, 16, 16); // Cut off object drawing outside 16x16 area
            cr.Clip();
            DrawObject(obj, cr, 9, 9);
        }


        // Private methods

        void DrawObject(ObjectDefinition obj, Cairo.Context cr, double x, double y) {
            if (obj.GetGameObject() != null) {
                try {
                    ObjectAnimationFrame o = obj.GetGameObject().DefaultAnimation.GetFrame(0);
                    o.Draw(cr, (int)x, (int)y);
                }
                catch(NoAnimationException) {
                    // No animation defined
                }
                catch(InvalidAnimationException) {
                    // Error parsing an animation; draw a blue X to indicate the error
                    int width = 16;
                    double xPos = x-width/2 + 0.5;
                    double yPos = y-width/2 + 0.5;

                    cr.SetSourceColor(CairoHelper.ConvertColor(System.Drawing.Color.Blue));
                    cr.MoveTo(xPos, yPos);
                    cr.LineTo(xPos+width-1, yPos+width-1);
                    cr.MoveTo(xPos+width-1, yPos);
                    cr.LineTo(xPos, yPos+width-1);
                    cr.Stroke();
                }
            }
        }
    }
}

