﻿using System;
using System.Text;
using Gtk;

using LynnaLib;
using Util;

namespace LynnaLab
{
    [System.ComponentModel.ToolboxItem(true)]
    public class TilesetEditor : Gtk.Bin
    {
        Tileset tileset;

        SubTileEditor subTileEditor;
        GfxViewer subTileGfxViewer;
        PaletteEditor paletteEditor;
        TilesetViewer tilesetviewer1;

        SpinButtonHexadecimal tilesetSpinButton = new SpinButtonHexadecimal();
        ComboBoxFromConstants seasonSelectionButton;
        Gtk.Box tilesetSpinButtonContainer, seasonContainer, seasonComboBoxContainer;
        Gtk.Box tilesetVreContainer, tilesetViewerContainer, subTileContainer, subTileGfxContainer;
        Gtk.Box paletteEditorContainer;
        Gtk.Label paletteFrameLabel;
        PriorityStatusbar statusbar1;
        ValueReferenceEditor tilesetVre;

        WeakEventWrapper<Tileset> tilesetEventWrapper = new WeakEventWrapper<Tileset>();

        public TilesetEditor(Tileset t)
        {
            Gtk.Builder builder = new Builder();
            builder.AddFromString(Helper.ReadResourceFile("LynnaLab.Glade.TilesetEditor.ui"));
            builder.Autoconnect(this);

            tilesetSpinButtonContainer = (Gtk.Box)builder.GetObject("tilesetSpinButtonContainer");
            seasonContainer = (Gtk.Box)builder.GetObject("seasonContainer");
            seasonComboBoxContainer = (Gtk.Box)builder.GetObject("seasonComboBoxContainer");
            tilesetVreContainer = (Gtk.Box)builder.GetObject("tilesetVreContainer");
            tilesetViewerContainer = (Gtk.Box)builder.GetObject("tilesetViewerContainer");
            subTileContainer = (Gtk.Box)builder.GetObject("subTileContainer");
            subTileGfxContainer = (Gtk.Box)builder.GetObject("subTileGfxContainer");
            paletteEditorContainer = (Gtk.Box)builder.GetObject("paletteEditorContainer");
            paletteFrameLabel = (Gtk.Label)builder.GetObject("paletteFrameLabel");

            base.Child = (Gtk.Widget)builder.GetObject("TilesetEditor");


            tilesetviewer1 = new TilesetViewer();
            tilesetviewer1.AddTileSelectedHandler(delegate (object sender, int index)
            {
                subTileEditor.SetTileIndex(index);
            });
            tilesetViewerContainer.Add(tilesetviewer1);

            subTileGfxViewer = new GfxViewer();
            subTileGfxViewer.SelectionColor = Color.FromRgb(0, 256, 0);
            subTileGfxViewer.AddTileSelectedHandler(delegate (object sender, int index)
            {
                if (subTileEditor != null)
                    subTileEditor.SubTileIndex = (byte)(index ^ 0x80);
            });
            subTileGfxContainer.Add(subTileGfxViewer);

            subTileEditor = new SubTileEditor(this);
            subTileContainer.Add(subTileEditor);

            paletteEditor = new PaletteEditor();
            paletteEditorContainer.Add(paletteEditor);

            tilesetSpinButton = new SpinButtonHexadecimal();
            tilesetSpinButtonContainer.Add(tilesetSpinButton);

            if (t.Project.Game == Game.Seasons)
            {
                seasonSelectionButton = new ComboBoxFromConstants(showHelp: false);
                seasonSelectionButton.SetConstantsMapping(t.Project.SeasonMapping);
                seasonSelectionButton.SpinButton.Adjustment.Upper = 3;
                seasonSelectionButton.ActiveValue = t.Season;
                seasonComboBoxContainer.Add(seasonSelectionButton);
            }
            else
                seasonContainer.Destroy();

            statusbar1 = new PriorityStatusbar();
            ((Gtk.Box)builder.GetObject("statusbarHolder")).Add(statusbar1);

            SetTileset(t);

            tilesetSpinButton.ValueChanged += TilesetChanged;
            if (t.Project.Game == Game.Seasons)
                seasonSelectionButton.Changed += TilesetChanged;

            tilesetEventWrapper.Bind<int>("TileModifiedEvent", OnTileModified);
            tilesetEventWrapper.Bind<EventArgs>("PaletteHeaderGroupModifiedEvent", OnPalettesChanged);
        }


        Project Project
        {
            get { return tileset.Project; }
        }

        public Tileset Tileset
        {
            get
            {
                return tileset;
            }
        }

        public int Season
        {
            get {
                if (Project.Game == Game.Seasons && Tileset.IsSeasonal)
                    return seasonSelectionButton.ActiveValue;
                else
                    return -1;
            }
        }


        void TilesetChanged(object sender, EventArgs args)
        {
            SetTileset(Project.GetTileset(tilesetSpinButton.ValueAsInt, Season));
        }

        void OnTileModified(object sender, int tile)
        {
            if (tile == subTileEditor.subTileViewer.TileIndex)
            {
                subTileEditor.OnTileModified();
            }
            tilesetviewer1.QueueDraw();
        }

        void OnPalettesChanged(object sender, EventArgs args)
        {
            var group = tileset.PaletteHeaderGroup;
            paletteEditor.PaletteHeaderGroup = group;
            if (group != null)
                paletteFrameLabel.Text = group.ConstantAliasName;
        }

        void SetTileset(Tileset t)
        {
            if (t == tileset)
                return;
            tileset = t;

            tilesetEventWrapper.ReplaceEventSource(tileset);

            subTileEditor.SetTileset(tileset);
            if (tileset != null)
            {
                subTileGfxViewer.SetGraphicsState(tileset.GraphicsState, 0x2000, 0x3000);
            }

            tilesetviewer1.SetTileset(tileset);

            ValueReferenceGroup vrg = tileset.GetValueReferenceGroup();
            if (tilesetVre == null)
            {
                tilesetVre = new ValueReferenceEditor(Project, vrg, 8);
                tilesetVreContainer.Add(tilesetVre);
            }
            else
                tilesetVre.ReplaceValueReferenceGroup(vrg);

            OnPalettesChanged(null, null);

            tilesetSpinButton.Value = tileset.Index;
            tilesetSpinButton.Adjustment.Upper = Project.NumTilesets - 1;

            // Statusbar text
            var references = t.GetReferences();
            if (references.Count == 0)
            {
                statusbar1.Set(0, $"Tileset {t.Index:x2} never used.");
            }
            else
            {
                int numReferences = references.Count;
                int maxRoomsToList = 15;
                int truncated = 0;
                if (references.Count > maxRoomsToList)
                {
                    truncated = references.Count - maxRoomsToList;
                    while (references.Count > maxRoomsToList)
                        references.RemoveAt(references.Count - 1);
                }
                var roomListAsString = new StringBuilder();
                foreach (Room r in references)
                {
                    roomListAsString.Append(", " + r.Index.ToString("x3"));
                }
                roomListAsString.Remove(0, 2);
                if (truncated != 0)
                    roomListAsString.Append($", {truncated} more");
                string statusbarString =
                    $"Tileset {t.Index:x2}: Used in {numReferences} rooms ({roomListAsString})";
                statusbar1.Set(0, statusbarString);
            }

            // Enable/disable season selection
            if (Project.Game != Game.Ages)
            {
                if (Tileset.IsSeasonal)
                    seasonContainer.Sensitive = true;
                else
                    seasonContainer.Sensitive = false;
            }
        }

        protected void OnOkButtonClicked(object sender, EventArgs e)
        {
            Parent.Hide();
            Parent.Dispose();
        }

        // Draws the tile with the ability to select the quadrant to change the
        // properties.
        class SubTileViewer : TileGridViewer
        {

            int _tileIndex;
            Tileset tileset;

            public delegate void TileChangedHandler();

            // Called when subtile properties are modified
            public event TileChangedHandler SubTileChangedEvent;

            public int TileIndex
            {
                get { return _tileIndex; }
                set
                {
                    if (_tileIndex != value)
                    {
                        _tileIndex = value;
                        QueueDraw();
                    }
                }
            }
            public byte SubTileFlags
            {
                get
                {
                    if (tileset == null)
                        return 0;
                    return tileset.GetSubTileFlags(TileIndex, SelectedX, SelectedY);
                }
                set
                {
                    if (tileset == null)
                        return;
                    tileset.SetSubTileFlags(TileIndex, SelectedX, SelectedY, value);
                }
            }
            public byte SubTileIndex
            {
                get
                {
                    if (tileset == null)
                        return 0;
                    return tileset.GetSubTileIndex(TileIndex, SelectedX, SelectedY);
                }
                set
                {
                    if (tileset == null)
                        return;
                    tileset.SetSubTileIndex(TileIndex, SelectedX, SelectedY, value);
                }
            }

            override protected Bitmap Image
            {
                get
                {
                    if (tileset == null)
                        return null;
                    return tileset.GetTileImage(TileIndex);
                }
            }

            public SubTileViewer() : base()
            {
                Width = 2;
                Height = 2;
                TileWidth = 8;
                TileHeight = 8;
                Scale = 3;
                Selectable = true;
                SelectedIndex = 0;

                // Better to allow only one thing (between the subtile viewer & the spinbuttons
                // controlling it) to be selectable. This way, you can select the "palette"
                // spinbutton and use the arrow keys to modify it, while using the mouse to select
                // other tiles quickly, without having to move the mouse back and forth.
                CanFocus = false;

                base.AddTileSelectedHandler(delegate (object sender, int index)
                {
                    SubTileChangedEvent();
                });
            }

            public void SetTileset(Tileset t)
            {
                tileset = t;
            }
        }

        // Draws the tile with red rectangle representing solidity.
        class SubTileCollisionEditor : TileGridViewer
        {

            int _tileIndex;
            Tileset tileset;

            public event System.Action CollisionsChangedHandler;

            public int TileIndex
            {
                get { return _tileIndex; }
                set
                {
                    if (_tileIndex != value)
                    {
                        _tileIndex = value;
                        QueueDraw();
                    }
                }
            }

            protected override Bitmap Image
            {
                get { return tileset?.GetTileImage(TileIndex); }
            }

            public SubTileCollisionEditor() : base()
            {
                Width = 2;
                Height = 2;
                TileWidth = 8;
                TileHeight = 8;
                Scale = 3;

                // On clicked...
                TileGridEventHandler callback = delegate (object sender, int index)
                {
                    // Toggle the collision of the subtile if it uses the
                    // "basic" collision mode (upper nibble is zero).
                    if ((tileset.GetTileCollision(TileIndex) & 0xf0) == 0)
                    {
                        SetBasicCollision(TileIndex, HoveringX, HoveringY,
                                !GetBasicCollision(TileIndex, HoveringX, HoveringY));
                    }
                };

                base.AddMouseAction(MouseButton.Any, MouseModifier.Any | MouseModifier.Drag, GridAction.Callback, callback);

                base.UseTileDrawer = true;
            }

            protected override void TileDrawer(int index, Cairo.Context cr)
            {
                int x = index % 2;
                int y = index / 2;
                if ((tileset.GetTileCollision(TileIndex) & 0xf0) == 0)
                {
                    if (GetBasicCollision(TileIndex, x, y))
                    {
                        Color c = Color.FromRgbaDbl(1.0, 0.0, 0.0, 0.5);
                        cr.SetSourceColor(c);
                        cr.Rectangle(0, 0, 8, 8);
                        cr.Fill();
                    }
                }
            }

            bool GetBasicCollision(int subTile, int x, int y)
            {
                return tileset.GetSubTileBasicCollision(subTile, x, y);
            }

            void SetBasicCollision(int subTile, int x, int y, bool val)
            {
                tileset.SetSubTileBasicCollision(subTile, x, y, val);
                if (CollisionsChangedHandler != null)
                {
                    CollisionsChangedHandler();
                }
            }

            public void SetTileset(Tileset t)
            {
                tileset = t;
            }
        }

        class SubTileEditor : Gtk.Bin
        {

            // Which tile is being edited
            public byte TileIndex
            {
                get
                {
                    return (byte)subTileViewer.TileIndex;
                }
            }
            // Which subtile makes up the selected part of the tile
            public byte SubTileIndex
            {
                get { return (byte)(subTileSpinButton.ValueAsInt); }
                set
                {
                    subTileSpinButton.Value = value;
                }
            }

            Tileset Tileset
            {
                get
                {
                    return tilesetEditor.Tileset;
                }
            }

            internal SubTileViewer subTileViewer;
            SubTileCollisionEditor subTileCollisionEditor;
            TilesetEditor tilesetEditor;

            SpinButton collisionSpinButton;

            SpinButton subTileSpinButton;
            SpinButton paletteSpinButton;
            CheckButton flipXCheckButton, flipYCheckButton;
            CheckButton priorityCheckButton;
            CheckButton bankCheckButton;

            public SubTileEditor(TilesetEditor tilesetEditor)
            {
                this.tilesetEditor = tilesetEditor;

                Gtk.Box tmpBox;

                Gtk.Box vbox = new Box(Gtk.Orientation.Vertical, 2);
                vbox.Spacing = 10;

                // Top row: 2 images of the tile, one for selecting, one to show
                // collisions

                subTileViewer = new SubTileViewer();

                subTileViewer.SubTileChangedEvent += delegate ()
                {
                    PullEverything();
                };

                subTileCollisionEditor = new SubTileCollisionEditor();
                subTileCollisionEditor.CollisionsChangedHandler += () =>
                {
                    PullEverything();
                };

                Gtk.Box hbox = new Gtk.Box(Gtk.Orientation.Horizontal, 2);
                hbox.Halign = Gtk.Align.Center;
                hbox.Add(subTileViewer);
                hbox.Add(subTileCollisionEditor);
                vbox.Add(hbox);

                // Next row: collision value

                collisionSpinButton = new SpinButtonHexadecimal(0, 255);
                collisionSpinButton.Digits = 2;
                collisionSpinButton.ValueChanged += delegate (object sender, EventArgs e)
                {
                    Tileset.SetTileCollision(TileIndex, (byte)collisionSpinButton.ValueAsInt);
                    subTileCollisionEditor.QueueDraw();
                };

                Gtk.Label collisionLabel = new Gtk.Label("Collisions");

                tmpBox = new Gtk.Box(Gtk.Orientation.Horizontal, 2);
                tmpBox.Add(collisionLabel);
                tmpBox.Add(collisionSpinButton);
                vbox.Add(tmpBox);

                // Next rows: subtile properties

                var table = new Gtk.Grid();
                table.ColumnSpacing = 6;
                table.RowSpacing = 6;

                subTileSpinButton = new SpinButtonHexadecimal(0, 255);
                subTileSpinButton.ValueChanged += delegate (object sender, EventArgs e)
                {
                    PushFlags();
                };
                paletteSpinButton = new SpinButton(0, 7, 1);
                paletteSpinButton.ValueChanged += delegate (object sender, EventArgs e)
                {
                    PushFlags();
                };
                flipXCheckButton = new Gtk.CheckButton();
                flipXCheckButton.Toggled += delegate (object sender, EventArgs e)
                {
                    PushFlags();
                };
                flipYCheckButton = new Gtk.CheckButton();
                flipYCheckButton.Toggled += delegate (object sender, EventArgs e)
                {
                    PushFlags();
                };
                priorityCheckButton = new Gtk.CheckButton();
                priorityCheckButton.Toggled += delegate (object sender, EventArgs e)
                {
                    PushFlags();
                };
                bankCheckButton = new Gtk.CheckButton();
                bankCheckButton.Toggled += delegate (object sender, EventArgs e)
                {
                    PushFlags();
                };
                bankCheckButton.Sensitive = false; // On second thought, nobody actually needs this

                Gtk.Label subTileLabel = new Gtk.Label("Subtile Index");
                Gtk.Label paletteLabel = new Gtk.Label("Palette");
                Gtk.Label flipXLabel = new Gtk.Label("Flip X");
                Gtk.Label flipYLabel = new Gtk.Label("Flip Y");
                Gtk.Label priorityLabel = new Gtk.Label("Priority");
                Gtk.Label bankLabel = new Gtk.Label("Bank (0/1)");

                paletteLabel.TooltipText = "Palette index (0-7)";
                paletteSpinButton.TooltipText = "Palette index (0-7)";

                priorityLabel.TooltipText = "Check to make colors 1-3 appear above sprites";
                priorityCheckButton.TooltipText = "Check to make colors 1-3 appear above sprites";

                bankLabel.TooltipText = "This should always be checked.";
                bankCheckButton.TooltipText = "This should always be checked.";

                int y = 0;

                table.Attach(subTileLabel, 0, y, 1, 1);
                table.Attach(subTileSpinButton, 1, y, 1, 1);
                y++;
                table.Attach(paletteLabel, 0, y, 1, 1);
                table.Attach(paletteSpinButton, 1, y, 1, 1);
                y++;
                table.Attach(flipXLabel, 0, y, 1, 1);
                table.Attach(flipXCheckButton, 1, y, 1, 1);
                y++;
                table.Attach(flipYLabel, 0, y, 1, 1);
                table.Attach(flipYCheckButton, 1, y, 1, 1);
                y++;
                table.Attach(priorityLabel, 0, y, 1, 1);
                table.Attach(priorityCheckButton, 1, y, 1, 1);
                y++;
                table.Attach(bankLabel, 0, y, 1, 1);
                table.Attach(bankCheckButton, 1, y, 1, 1);
                y++;

                vbox.Add(table);
                this.Add(vbox);

                ShowAll();

                PullEverything();
            }

            public void SetTileset(Tileset t)
            {
                subTileViewer.SetTileset(t);
                subTileCollisionEditor.SetTileset(t);
                PullEverything();
            }

            public void SetTileIndex(int tile)
            {
                subTileViewer.TileIndex = tile;
                subTileCollisionEditor.TileIndex = tile;
                PullEverything();
            }

            public void OnTileModified()
            {
                subTileViewer.QueueDraw();
                subTileCollisionEditor.QueueDraw();
            }

            void PullEverything()
            {
                if (Tileset != null)
                {
                    collisionSpinButton.Value = Tileset.GetTileCollision(TileIndex);
                    subTileCollisionEditor.QueueDraw();
                }

                byte flags = subTileViewer.SubTileFlags;

                subTileSpinButton.Value = subTileViewer.SubTileIndex;
                tilesetEditor.subTileGfxViewer.SelectedIndex = subTileViewer.SubTileIndex ^ 0x80;

                paletteSpinButton.Value = flags & 7;
                flipXCheckButton.Active = ((flags & 0x20) != 0);
                flipYCheckButton.Active = ((flags & 0x40) != 0);
                priorityCheckButton.Active = ((flags & 0x80) != 0);
                bankCheckButton.Active = ((flags & 0x08) != 0);
            }

            void PushFlags()
            {
                byte flags = 0;
                flags |= (byte)paletteSpinButton.ValueAsInt;
                if (flipXCheckButton.Active)
                    flags |= 0x20;
                if (flipYCheckButton.Active)
                    flags |= 0x40;
                if (priorityCheckButton.Active)
                    flags |= 0x80;
                if (bankCheckButton.Active)
                    flags |= 0x08;

                subTileViewer.SubTileFlags = flags;
                subTileViewer.SubTileIndex = (byte)(subTileSpinButton.ValueAsInt);
                tilesetEditor.subTileGfxViewer.SelectedIndex = subTileViewer.SubTileIndex ^ 0x80;
            }
        }
    }
}
