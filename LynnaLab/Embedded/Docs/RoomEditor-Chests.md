# Room Editor: Chests
You can use the chest editor to place one (1) chest in a room. In addition to adding the chest in the chest editor, you must ALSO place a chest tile in the room.

The game remembers that the chest has been opened using ROOMFLAG_ITEM (see [room flags](https://wiki.zeldahacking.net/oracle/Room_flags)).

## Chest Data
  * ID: Determines what treasure you get.
  * SubID: Determines the variant of the treasure you get.

The ID and SubID are paired together to reference an instance of "Treasure Object Data" (see [disasm:data/{game}/treasureObjectData.s]())

## Treasure Object Data
Note that multiple different chests (or even scripts!) can refer to the same treasure object data, so take care when modifying this. When in doubt, increase the SubID to a new, unused value and modify that freely.

  * Spawn Mode: How the items spawns in. This **MUST** be set to either *FROM_CHEST_A* or *FROM_CHEST_B*, otherwise the game will softlock when the chest is opened! (Other values are for use in scripts.)
  * Grab Mode: Link's animation when he gets the item. (IIRC this has no effect on items obtained from chests?)
  * Parameter: This is a nebulous name for a nebulous value. It can mean:
    * Item level
    * Number of the item to get (bombs / seeds / etc)
    * Ring index
    * Seasons for the rod
    * And more...
  * Graphics: What it looks like. Represents an index in the table "interaction60SubidData" in [disasm:data/{game}/interactionData.s]().
  * Text Index: Text to show, ie. TX_00XX (must always start with 00).
  * Set "Item Obtained" flag: Determines whether to set ROOMFLAG_ITEM. This is basically required for treasures that come from chests, unless you want the chest to be infinitely respawning.

## Multiple chests in a room
If you want to have multiple chests in a room, it can be done, but you need to fake it using scripts. (At some point I need to update the [tutorials](https://wiki.zeldahacking.net/oracle/Tutorials) page with this!)
