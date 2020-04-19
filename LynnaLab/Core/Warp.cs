using System;
using System.IO;
using System.Collections.Generic;

namespace LynnaLab
{
    // This class is an abstraction of WarpSourceData and WarpDestData, managed by the WarpGroup
    // class. It hides annoying details such as managing warp destination indices manually.
    public class Warp
    {
        ValueReferenceGroup vrg;


        // Constructors
        internal Warp(WarpGroup group, WarpSourceData data) {
            SourceGroup = group;
            SourceData = data;

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

        public int Opcode {
            get {
                return SourceData.Opcode;
            }
            set {
                SourceData.Opcode = value;
            }
        }
        public bool TopLeft {
            get {
                return SourceData.TopLeft;
            }
            set {
                SourceData.TopLeft = value;
            }
        }
        public bool TopRight {
            get {
                return SourceData.TopRight;
            }
            set {
                SourceData.TopRight = value;
            }
        }
        public bool BottomLeft {
            get {
                return SourceData.BottomLeft;
            }
            set {
                SourceData.BottomLeft = value;
            }
        }
        public bool BottomRight {
            get {
                return SourceData.BottomRight;
            }
            set {
                SourceData.BottomRight = value;
            }
        }
        public int SourceTransition {
            get {
                return SourceData.Transition;
            }
            set {
                SourceData.Transition = value;
            }
        }
        public int SourceX {
            get {
                return SourceData.X;
            }
            set {
                SourceData.X = value;
            }
        }
        public int SourceY {
            get {
                return SourceData.Y;
            }
            set {
                SourceData.Y = value;
            }
        }

        public bool HasEdgeWarp {
            get { return (Opcode & 0x0f) != 0; }
        }

        // Propreties from warp destination

        public int DestRoomIndex {
            get {
                return (SourceData.DestGroupIndex << 8) | DestData.Map;
            }
            set {
                if (DestRoomIndex != value) {
                    if (SourceData.DestGroupIndex != value >> 8) { // Group changed
                        IsolateDestData(value >> 8);
                    }
                    else
                        IsolateDestData();
                    DestData.Map = value & 0xff;
                }
            }
        }

        public Room DestRoom {
            get { return Project.GetIndexedDataType<Room>(DestRoomIndex); }
            set { DestRoomIndex = value.Index; }
        }

        public int DestY {
            get { return DestData.Y; }
            set {
                if (DestY != value) {
                    IsolateDestData();
                    DestData.Y = value;
                }
            }
        }
        public int DestX {
            get { return DestData.X; }
            set {
                if (DestX != value) {
                    IsolateDestData();
                    DestData.X = value;
                }
            }
        }
        public int DestParameter {
            get { return DestData.Parameter; }
            set {
                if (DestParameter != value) {
                    IsolateDestData();
                    DestData.Parameter = value;
                }
            }
        }
        public int DestTransition {
            get { return DestData.Transition; }
            set {
                if (DestTransition != value) {
                    IsolateDestData();
                    DestData.Transition = value;
                }
            }
        }

        // Other properties


        public WarpGroup SourceGroup { get; private set; }

        // Underlying warp source data object. In general, manipulating this directly
        // isn't recommended.
        internal WarpSourceData SourceData { get; private set; }

        WarpDestData DestData { get { return SourceData.GetReferencedDestData(); } }

        ValueReferenceGroup SourceVrg { get { return SourceData.ValueReferenceGroup; } }
        ValueReferenceGroup DestVrg { get { return DestData.ValueReferenceGroup; } }


        public void Remove() {
            SourceGroup.RemoveWarp(this);
        }


        // ValueReferenceGroup for simpler editing based on a few named parameters
        ValueReferenceGroup ConstructValueReferenceGroup() {
            var valueReferences = new List<ValueReference>();

            ValueReference vref;

            vref = new AbstractIntValueReference(Project,
                    name: "Opcode",
                    type: ValueReferenceType.Int,
                    getter: () => Opcode,
                    setter: (value) => Opcode = value,
                    maxValue: 255);
            vref.Editable = false;
            valueReferences.Add(vref);

            if (WarpSourceType == WarpSourceType.Standard) {
                vref = new AbstractIntValueReference(Project,
                        name: "Top-Left",
                        type: ValueReferenceType.Bool,
                        getter: () => TopLeft ? 1 : 0,
                        setter: (value) => TopLeft = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Top-Right",
                        type: ValueReferenceType.Bool,
                        getter: () => TopRight ? 1 : 0,
                        setter: (value) => TopRight = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Bottom-Left",
                        type: ValueReferenceType.Bool,
                        getter: () => BottomLeft ? 1 : 0,
                        setter: (value) => BottomLeft = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Bottom-Right",
                        type: ValueReferenceType.Bool,
                        getter: () => BottomRight ? 1 : 0,
                        setter: (value) => BottomRight = value == 0 ? false : true,
                        maxValue: 1);
                valueReferences.Add(vref);
            }
            else if (WarpSourceType == WarpSourceType.Pointed) {
                vref = new AbstractIntValueReference(Project,
                        name: "Source Y",
                        type: ValueReferenceType.Int,
                        getter: () => SourceY,
                        setter: (value) => SourceY = value,
                        maxValue: 15);
                valueReferences.Add(vref);

                vref = new AbstractIntValueReference(Project,
                        name: "Source X",
                        type: ValueReferenceType.Int,
                        getter: () => SourceX,
                        setter: (value) => SourceX = value,
                        maxValue: 15);
                valueReferences.Add(vref);
            }
            else
                throw new Exception("Invalid warp source type for warp.");

            vref = new AbstractIntValueReference(Project,
                    name: "Source Transition",
                    type: ValueReferenceType.Int,
                    getter: () => SourceTransition,
                    setter: (value) => SourceTransition = value,
                    maxValue: 15,
                    constantsMappingString: "SourceTransitionMapping");
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Room",
                    type: ValueReferenceType.Int,
                    getter: () => DestRoomIndex,
                    setter: (value) => DestRoomIndex = value,
                    maxValue: Project.GetNumRooms()-1); // TODO: seasons has some "gap" rooms
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Y",
                    type: ValueReferenceType.Int,
                    getter: () => DestY,
                    setter: (value) => DestY = value,
                    maxValue: 15);
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest X",
                    type: ValueReferenceType.Int,
                    getter: () => DestX,
                    setter: (value) => DestX = value,
                    maxValue: 15);
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Parameter",
                    type: ValueReferenceType.Int,
                    getter: () => DestParameter,
                    setter: (value) => DestParameter = value,
                    maxValue: 15);
            valueReferences.Add(vref);

            vref = new AbstractIntValueReference(Project,
                    name: "Dest Transition",
                    type: ValueReferenceType.Int,
                    getter: () => DestTransition,
                    setter: (value) => DestTransition = value,
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
                if (newGroup >= Project.GetNumGroups())
                    throw new Exception(string.Format("Group {0} is too high for warp destination.", newGroup));
                var destGroup = Project.GetIndexedDataType<WarpDestGroup>(newGroup);
                SourceData.SetDestData(destGroup.GetNewOrUnusedDestData());
            }
            else {
                if (DestData.GetNumReferences() != 1) { // Used by another warp source
                    SourceData.SetDestData(SourceData.DestGroup.GetNewOrUnusedDestData());
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
    }
}
