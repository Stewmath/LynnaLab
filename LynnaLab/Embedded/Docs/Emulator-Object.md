# Emulator tab: Objects
You can view the object list and the memory of a specific object in this tab.

In addition, if the "Objects" checkbox above the game screen is checked, rectangles will be drawn around every object. You can click and drag these rectangles to move these objects around. Clicking on an object will also select it in this tab.

## Object List
The object list is shown if no object is selected. All 64 object slots are shown, with 16 slots per column.

Left-click on any object name in a column to go to the object info view.

Right-click on an empty slot to spawn an object in that slot, or to delete an object in an occupied slot.

When spawning an object, you must specify an ID (the object to spawn), and sometimes a subID is also needed to set the variant. "Var03" is rarely required, you can leave that set to 00 in most cases.

In the interaction column only, you have an option to spawn a treasure object. This is provided for convenience (a treasure object is just a specific type of interaction).

## Object Info
This view shows the information for one specific, selected object.

Each object has 64 bytes of dedicated memory, which you can edit here. Click on any byte and start typing numbers in order to edit memory.

Click the "back" button to go back to the object list.

## More information
For more information, see:

[Wiki: Objects](https://wiki.zeldahacking.net/oracle/Objects)
