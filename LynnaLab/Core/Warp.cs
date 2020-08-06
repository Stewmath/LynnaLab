using System;
using System.IO;
using System.Collections.Generic;
using Util;

namespace LynnaLab
{
    // This class is an abstraction of WarpSourceData and WarpDestData, managed by the WarpGroup
    // class. It hides annoying details such as managing warp destination indices manually.
    public class Warp
    {
        ValueReferenceGroup vrg;

        LockableEvent<EventArgs> ModifiedEvent = new LockableEvent<EventArgs>();


        // Constructors
        internal Warp(WarpGroup group, WarpSourceData data) {
            SourceGroup = group;
            SourceData = data;

            SourceData.AddModifiedEventHandler(OnDataModified);

            vrg = ConstructValueReferenceGroup();
        }


        // Properties from warp source

        public Project Project {
            get { return SourceData.Project; }
        }
        public WarpSourceType WarpSourceType {
            get { return SourceData.WarpSourceType; }
        }
        public ValueReferenceGroup ValueReferenceGroup {
            get { return vrg; }
        }

        public bool TopLeft {
            get {
                return vrg.GetIntValue("Top-Left") != 0;
            }
            set {
                vrg.SetValue("Top-Left", value ? 1 : 0);
            }
        }
        public bool TopRight {
            get {
                return vrg.GetIntValue("Top-Right") != 0;
            }
            set {
                vrg.SetValue("Top-Right", value ? 1 : 0);
            }
        }
        public bool BottomLeft {
            get {
                return vrg.GetIntValue("Bottom-Left") != 0;
            }
            set {
                vrg.SetValue("Bottom-Left", value ? 1 : 0);
            }
        }
        public bool BottomRight {
            get {
                return vrg.GetIntValue("Bottom-Right") != 0;
            }
            set {
                vrg.SetValue("Bottom-Right", value ? 1 : 0);
            }
        }
        public int SourceTransition {
            get {
                return vrg.GetIntValue("Source Transition");
            }
            set {
                vrg.SetValue("Source Transition", value);
            }
        }
        public int SourceX {
            get {
                return vrg.GetIntValue("Source X");
            }
            set {
                vrg.SetValue("Source X", value);
            }
        }
        public int SourceY {
            get {
                return vrg.GetIntValue("Source Y");
            }
            set {
                vrg.SetValue("Source Y", value);
            }
        }

        public Room SourceRoom {
            get {
                return SourceGroup.Room;
            }
        }

        public bool HasEdgeWarp {
            get { return (Opcode & 0x0f) != 0; }
        }

        int Opcode {
            get { return SourceData.Opcode; }
        }

        // Propreties from warp destination

        public int DestRoomIndex {
            get {
                return vrg.GetIntValue("Dest Room");
            }
            set {
                vrg.SetValue("Dest Room", value);
            }
        }

        public Room DestRoom {
            get { return Project.GetIndexedDataType<Room>(DestRoomIndex); }
            set { DestRoomIndex = value.Index; }
        }

        public int DestY {
            get {
                return vrg.GetIntValue("Dest Y");
            }
            set {
                vrg.SetValue("Dest Y", value);
            }
        }
        public int DestX {
            get {
                return vrg.GetIntValue("Dest X");
            }
            set {
                vrg.SetValue("Dest X", value);
            }
        }
        public int DestParameter {
            get {
                return vrg.GetIntValue("Dest Parameter");
            }
            set {
                vrg.SetValue("Dest Parameter", value);
            }
        }
        public int DestTransition {
            get {
                return vrg.GetIntValue("Dest Transition");
            }
            set {
                vrg.SetValue("Dest Transition", value);
            }
        }

        // Other properties


        public WarpGroup SourceGroup { get; private set; }

        // Underlying warp source data object. In general, manipulating this directly
        // isn't recommended; direct modifications to the base data don't trigger event handlers
        // set by the "AddModifiedHandler" function. Same with "DestData".
        internal WarpSourceData SourceData { get; private set; }

        WarpDestData DestData { get { return SourceData.GetReferencedDestData(); } }

        ValueReferenceGroup SourceVrg { get { return SourceData.ValueReferenceGroup; } }
        ValueReferenceGroup DestVrg { get { return DestData.ValueReferenceGroup; } }


        public void Remove() {
            SourceGroup.RemoveWarp(this);
        }

        public void AddModifiedHandler(EventHandler<EventArgs> handler) {
            ModifiedEvent += handler;
        }

        public void RemoveModifiedHandler(EventHandler<EventArgs> handler) {
            ModifiedEvent -= handler;
        }


        // ValueReferenceGroup for simpler editing based on a few named parameters.
        // All modifications to the underlying data should be done through the ValueReferenceGroup
        // when possible, so that its "value changed" event handlers are properly invoked.
        ValueReferenceGroup ConstructValueReferenceGroup() {
            var valueReferences = new List<ValueReference>();

            ValueReference vref;

            vref = new AbstractIntValueReference(Project,
                    name: "Opcode",
                    type: ValueReferenceType.Int,
                    getter: () => SourceData.Opcode,
                    setter: (value) => {},
                    maxValue: 255);
            vref.Editable = false;
            valueReferences.Add(vref);

            if (WarpSourceType == WarpSourceType.Standard) {
                vref = new AbstractIntValueReference(Project,
                        name: "Top-Left",
                        type: ValueReferenceType.Bool,
                        getter: () => SourceData.TopLeft ? 1 : 0,
                        setter: (value) => SourceData.TopLeft = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Top-Right",
                        type: ValueReferenceType.Bool,
                        getter: () => SourceData.TopRight ? 1 : 0,
                        setter: (value) => SourceData.TopRight = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Bottom-Left",
                        type: ValueReferenceType.Bool,
                        getter: () => SourceData.BottomLeft ? 1 : 0,
                        setter: (value) => SourceData.BottomLeft = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Bottom-Right",
                        type: ValueReferenceType.Bool,
                        getter: () => SourceData.BottomRight ? 1 : 0,
                        setter: (value) => SourceData.BottomRight = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);
            }
            else if (WarpSourceType == WarpSourceType.Pointed) {
                vref = new AbstractIntValueReference(Project,
                        name: "Source Y",
                        type: ValueReferenceType.Int,
                        getter: () => SourceData.Y,
                        setter: (value) => SourceData.Y = value,
                        maxValue: 15);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Source X",
                        type: ValueReferenceType.Int,
                        getter: () => SourceData.X,
                        setter: (value) => SourceData.X = value,
                        maxValue: 15);
                valueReferences.Add(vref);
            }
            else
                throw new Exception("Invalid warp source type for warp.");

            vref = new AbstractIntValueReference(Project,
                    name: "Source Transition",
                    type: ValueReferenceType.Int,
                    getter: () => SourceData.Transition,
                    setter: (value) => SourceData.Transition = value,
                    maxValue: 15,
                    constantsMappingString: "SourceTransitionMapping");
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Room",
                    type: ValueReferenceType.Int,
                    getter: () => (SourceData.DestGroupIndex << 8) | DestData.Map,
                    setter: (value) => {
                        if (DestRoomIndex != value) {
                            if (SourceData.DestGroupIndex != value >> 8) { // Group changed
                                IsolateDestData(value >> 8);
                            }
                            else
                                IsolateDestData();
                            DestData.Map = value & 0xff;
                        }
                    },
                    maxValue: Project.NumRooms-1); // TODO: seasons has some "gap" rooms
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Y",
                    type: ValueReferenceType.Int,
                    getter: () => DestData.Y,
                    setter: (value) => {
                        if (DestData.Y != value) {
                            IsolateDestData();
                            DestData.Y = value;
                        }
                    },
                    maxValue: 15);
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest X",
                    type: ValueReferenceType.Int,
                    getter: () => DestData.X,
                    setter: (value) => {
                        if (DestData.X != value) {
                            IsolateDestData();
                            DestData.X = value;
                        }
                    },
                    maxValue: 15);
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Parameter",
                    type: ValueReferenceType.Int,
                    getter: () => DestData.Parameter,
                    setter: (value) => {
                        if (DestData.Parameter != value) {
                            IsolateDestData();
                            DestData.Parameter = value;
                        }
                    },
                    maxValue: 15);
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Transition",
                    type: ValueReferenceType.Int,
                    getter: () => DestData.Transition,
                    setter: (value) => {
                        if (DestData.Transition != value) {
                            IsolateDestData();
                            DestData.Transition = value;
                        }
                    },
                    maxValue: 15,
                    constantsMappingString: "DestTransitionMapping");
            valueReferences.Add(vref);


            return new ValueReferenceGroup(valueReferences);
        }


        // Call this to ensure that the destination data this warp uses is not also used by anything
        // else. If it is, we find unused dest data or create new data.
        void IsolateDestData(int newGroup = -1) {
            if (newGroup == -1)
                newGroup = SourceData.DestGroupIndex;

            WarpDestData oldDest = DestData;
            if (newGroup != SourceData.DestGroupIndex) {
                if (newGroup >= Project.NumGroups)
                    throw new Exception(string.Format("Group {0} is too high for warp destination.", newGroup));
                var destGroup = Project.GetIndexedDataType<WarpDestGroup>(newGroup);
                SetDestData(destGroup.GetNewOrUnusedDestData());
            }
            else {
                if (DestData.GetNumReferences() != 1) { // Used by another warp source
                    SetDestData(SourceData.DestGroup.GetNewOrUnusedDestData());
                }
            }

            if (oldDest != DestData) {
                DestData.Map = oldDest.Map;
                DestData.Y = oldDest.Y;
                DestData.X = oldDest.X;
                DestData.Parameter = oldDest.Parameter;
                DestData.Transition = oldDest.Transition;
            }

            if (DestData.GetNumReferences() != 1)
                throw new Exception("Internal error: New warp destination has "
                        + DestData.GetNumReferences() + " references.");
        }

        // Always use this function when changing the DestData so that modified handlers are updated
        void SetDestData(WarpDestData newDestData) {
            DestData.RemoveModifiedEventHandler(OnDataModified);
            SourceData.SetDestData(newDestData);
            DestData.AddModifiedEventHandler(OnDataModified);
        }


        void OnDataModified(object sender, DataModifiedEventArgs args) {
            ModifiedEvent.Invoke(this, null);
        }
    }
}
