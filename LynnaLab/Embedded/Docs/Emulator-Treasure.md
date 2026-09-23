# Emulator tab: Treasure
This tab has 3 sub-tabs for treasure management.

## Give/Lose
Select a treasure ID and a parameter. Then, click on the "Give treasure" button to instantly give that treasure, with that parameter, to Link. (Parameter can be level, quantity, etc...)

You can click "Lose treasure" to take away the treasure (the parameter has no effect).

This does not spawn a treasure object (no visual cue); it writes directly into Link's inventory and wObtainedTreasureFlags by calling the "giveTreasure" or "loseTreasure" functions.

Depending on the treasure, this tab can also show the current value of the "parameter", ie. "wNumBombs" for TREASURE_BOMBS. Editing this value affects the live game state. (Note, this is not working properly for dungeon items like small keys, etc! Just use the general tab.)

## Obtained Treasure Flags
This displays the contents of the wObtainedTreasureFlags bit array. Check or uncheck the box to set or unset the corresponding bit.

This works best for items on the inventory subscreen (ie. ribbon, bell, etc). For equippable items, typically they need to both have the "obtained treasure flag" bit set in addition to existing in your inventory (which this does not affect). In that case it's better to use the "Give/Lose" tab, which updates both.

## Rings
You can set the rings that Link has available in Vasu's shop.

You can instead do Give/Lose Treasure with TREASURE_RING and the parameter set to the corresponding ring index, but that will add the ring to Link's unappraised list instead of being appraised.

## More information
For more information, see: [Wiki: Treasure](https://wiki.zeldahacking.net/oracle/Treasure).
