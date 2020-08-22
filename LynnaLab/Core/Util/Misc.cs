using Collections = System.Collections.Generic;


public static class Extensions {
    // For SOME REASON the "IReadOnlyList" class is missing some methods that "List" has. Add them
    // here.
    public static int IndexOf<T>(this Collections.IReadOnlyList<T> self, T elementToFind)
    {
        int i = 0;
        foreach (T element in self)
        {
            if (Equals(element, elementToFind))
                return i;
            i++;
        }
        return -1;
    }

    public static bool Contains<T>(this Collections.IReadOnlyList<T> self, T elementToFind)
    {
        return self.IndexOf(elementToFind) != -1;
    }
}
