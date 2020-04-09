using System;
using System.Drawing;
using Gtk;
using Cairo;

namespace LynnaLab {

    public class ObjectBox : TileGridViewer {
        ObjectGroup objectGroup;


        public ObjectGroup ObjectGroup { get { return objectGroup; } }


        // Constructors

        public ObjectBox(ObjectGroup group) : base() {
            objectGroup = group;

            base.Width = 8;
            base.Height = 2;
            base.TileWidth = 18;
            base.TileHeight = 18;
            base.TilePaddingX = 2;
            base.TilePaddingY = 2;

            base.Selectable = true;
            base.MaxIndex = objectGroup.GetNumObjects() - 1;

            objectGroup.ModifiedEvent += (sender, args) => {
                RedrawAll();
                base.MaxIndex = objectGroup.GetNumObjects() - 1;
            };

            TileGridEventHandler dragCallback = (sender, index) => {
                if (index != SelectedIndex) {
                    objectGroup.MoveObject(SelectedIndex, index);
                    SelectedIndex = index;
                }
            };


            base.AddMouseAction(MouseButton.LeftClick, MouseModifier.Any | MouseModifier.Drag,
                    GridAction.Callback, dragCallback);

            ButtonPressEventHandler OnButtonPressEvent = delegate(object sender, ButtonPressEventArgs args) {
                int x,y;
                Gdk.ModifierType state;
                args.Event.Window.GetPointer(out x, out y, out state);

                if (state.HasFlag(Gdk.ModifierType.Button3Mask)) {
                    if (HoveringIndex != -1)
                        SelectedIndex = HoveringIndex;
                    ShowPopupMenu(args.Event);
                }
            };

            this.ButtonPressEvent += new ButtonPressEventHandler(OnButtonPressEvent);

            RedrawAll();
        }


        // Methods

        public void SetSelectedIndex(int index) {
            SelectedIndex = index;
        }

        void ShowPopupMenu(Gdk.EventButton ev) {
            Gtk.Menu menu = new Gtk.Menu();

            for (int i=0; i<ObjectGroupEditor.ObjectNames.Length; i++) {
                if (i >= 2 && i <= 4) // Skip "Pointer" objects
                    continue;
                Gtk.MenuItem item = new Gtk.MenuItem("Add " + ObjectGroupEditor.ObjectNames[i]);
                menu.Append(item);

                int index = i;

                item.Activated += (sender, args) => {
                    SetSelectedIndex(objectGroup.AddObject((ObjectType)index));
                };
            }

            if (HoveringIndex != -1) {
                menu.Append(new Gtk.SeparatorMenuItem());

                Gtk.MenuItem deleteItem = new Gtk.MenuItem("Delete");
                deleteItem.Activated += (sender, args) => {
                    if (SelectedIndex != -1)
                        objectGroup.RemoveObject(SelectedIndex);
                };
                menu.Append(deleteItem);
            }

            menu.AttachToWidget(this, null);
            menu.ShowAll();
            menu.Popup(null, null, null, IntPtr.Zero, ev.Button, ev.Time);
        }


        Bitmap TileDrawer(int index) {
            if (index >= objectGroup.GetNumObjects())
                return null;

            Bitmap bitmap = new Bitmap(18, 18);

            using (Cairo.Context cr = new BitmapContext(bitmap)) {
                ObjectDefinition obj = objectGroup.GetObject(index);
                cr.SetSourceColor(ObjectGroupEditor.GetObjectColor(obj.GetObjectType()));
                cr.Rectangle(0, 0, 18, 18);
                cr.Fill();
                cr.Rectangle(1, 1, 16, 16); // Cut off object drawing outside 16x16 area
                cr.Clip();
                DrawObject(obj, cr, 9, 9);
            }
            return bitmap;
        }

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

        void RedrawAll() {
            base.DrawImageWithTiles(this.TileDrawer);
        }
    }
}

