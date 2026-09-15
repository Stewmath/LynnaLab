# Room Editor
The Room Editor is the most fundamental interface in LynnaLab. It's split into a left, center, and right pane.

## Left pane: Mode select
In the left pane, you can select between several editing modes:
  * [Room/Tile Editing](RoomEditor-Rooms.md)
  * [Object Editing](RoomEditor-Objects.md)
  * [Warp Editing](RoomEditor-Warps.md)
  * [Chest Editing](RoomEditor-Chests.md)

## Center pane: Room preview
In the center pane is a preview of the room that was selected. Here you can visually manipulate tiles, objects, warps, etc.

## Right pane: Room selector
This pane contains a minimap previewing an entire overworld or dungeon, through which you can select which room you want to be editing.

You can switch between overworlds and dungeons. These are technically distinct from each other:

  * One overworld, or "group", represents a fixed, 16x16 grid of rooms.
  * A dungeon is an 8x8 grid of rooms which is rearrangeable. It's possible for one room to exist at multiple positions within a dungeon. However, this typically only applies to "room 0", aka the blank placeholder room.

The reason for this distinction is that dungeons are irregularly shaped, and do not require unique rooms at each location in the grid (this would be a waste of space in the ROM).

All dungeon rooms also exist as overworld rooms; dungeons simply borrow rooms from those overworlds.

### What is a group?
A "group" is a group of 256 rooms. LynnaLab displays the selected group as a 16x16 grid.

A room number consists of 3 digits, ie. "3CF". The first digit always corresponds to the group number, in this case, "3"; the next two digits (0xCF) are the room index within group 3.

In-game, the current group number is tracked in the "wActiveGroup" variable and is fundamental to how rooms are managed.

See also: [Wiki: Group numbers](https://wiki.zeldahacking.net/oracle/Group_number)

### Group duplicates
What's peculiar is that overworld groups 4/6 and 5/7 are duplicates of each other - the same rooms exist in each! The reasons for this are a bit complicated, but the gist of it is that sidescrolling rooms always use group number 6 or 7, while normal rooms use group number 4 or 5.

This can be pretty nasty, because duplicate rooms in different groups actually have **separate** warp data, but share the same room layout! This can lead to an utterly bizarre "phantom room" phenomenon where, in-game, you appear to be in the correct room, but the group number is wrong, meaning warps don't work correctly.

These duplicates are frustrating because there is usually only one "canonical" version that you should be using. LynnaLab has two mechanisms to deal with this situation:
  * The "Darken duplicate rooms" setting darkens the "non-canonical" versions of the room. In addition to darkening dungeon rooms on the overworld tab (because they should be viewed in their respective dungeon), this will darken sidescrolling rooms in groups 4/5, or normal rooms in groups 6/7. (This setting only applies to the overworld tab, not the dungeon tab.)
  * The "Auto-adjust group number" setting will automatically change which group is selected. For example, if you are in group 4 in a top-down room, but then select a room on the minimap which is sidescrolling room, it will automatically change the group number to "6" which sends you to the "canonical" version of that room. (This setting only applies to the overworld tab; it is effectively always on in the dungeon tab.)

Overall, this should make the editing experience mostly sane. But one caveat is that changing the tileset used by a room (ie. changing it into a sidescrolling tileset) does NOT automatically adjust the group number; for that, you need to reselect the room.

### Seasons: Groups 1, 2, 3
In Seasons, groups 1, 2, and 3 are duplicates of each other, much like groups 4/6 and 5/7 are in both games. Again, all groups share the same room layouts. But their warps are different, and there are other differences, ie. the subrosia minimap only works in group 1.

Everything mentioned above applies here: you can use the "darken duplicate rooms" and "auto-adjust group number" settings to help you manage these rooms.

LynnaLab uses the following heuristic to determine the "canonical" group for a room (all others will be darkened):

  * (Seasons) If the room's tileset has the "subrosia" flag set, it's group 1.
  * (Seasons) If the room's tileset has the "maku tree" flag set, it's group 2.
  * (Seasons) If the room's tileset has the "small indoor" flag set, it's group 3.
  * (Groups 4+ only) If the room's tileset has the "sidescrolling" flag set, it's group 6 or 7, otherwise it's group 4 or 5.
  * Otherwise, it does not attempt any corrections.

...All this because the game misuses the "group number" variable to check for conditions that are already defined by the tileset!
