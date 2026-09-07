# Room Wiper
The room wiper allows you to clear data in all rooms.

## Limitations
The Room Wiper does not wipe:

  * Tile changes (data/{game}/singleTileChanges.s, data/{game}/roomSpecificTileChanges.s)
    * TODO: Hyperlink
  * Hardcoded stuff (code that uses ROOM\_AGES\_XXX or ROOM\_SEASONS\_XXX constants)
  * Anything that the Region Editor handles (ie. tileset/music assignments)
