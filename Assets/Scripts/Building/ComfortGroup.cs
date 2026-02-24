namespace ByteWar.Building
{
    /// <summary>
    /// Comfort group categories. Only one piece per group contributes to comfort.
    /// Valheim-style: multiple fireplaces don't stack, but fireplace + bed + banner all count.
    /// </summary>
    public enum ComfortGroup
    {
        None = 0,
        Fire = 1,
        Bed = 2,
        Banner = 3,
        Table = 4,
        Chair = 5,
        Rug = 6,
        Shelf = 7,
    }
}
