public enum Game
{
    Seasons = 0,
    Ages
}

public enum Season
{
    None = -1,
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3,
}

public enum Companion
{
    None = -1,
    Ricky = 0,
    Dimitri = 1,
    Moosh = 2,
}

public static class EnumExtensions
{
    public static String GetString(this Companion companion)
    {
        switch (companion)
        {
            case Companion.Ricky:
                return "Ricky";
            case Companion.Dimitri:
                return "Dimitri";
            case Companion.Moosh:
                return "Moosh";
            default:
                throw new Exception($"Invalid companion {companion}");
        }
    }
}
