# Room Wiper
The room wiper allows you to clear data in a selection of rooms, leaving you with a "mostly" blank slate to work with.

## Limitations
The Room Wiper doesn't leave everything as an entirely blank slate, for a few reasons.

1. Anything that the [region editor](RegionEditor.md) deals with is out of scope (ie. tileset/music assignments).

2. There is some data which LynnaLab just doesn't handle at all. See: [Manual Data Editing](ManualData.md) for some of these cases. (Look out for room-specific tile changes in particular.)

3. There are also some hardcoded events, ie. code that uses the constants "ROOM_AGES_XXX" or "ROOM_SEASONS_XXX" to do specific things in specific rooms, which can cause surprises even after wiping rooms clean. (One example: You can't use the Cane of Somaria in Patch's room, ROOM_AGES_5e8.)

