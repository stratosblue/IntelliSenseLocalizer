namespace System.Collections.Generic;

public static class HashSetExtensions
{
    public static HashSet<T> AddRange<T>(this HashSet<T> hashSet, IEnumerable<T> collection)
    {
        foreach (var item in collection)
        {
            hashSet.Add(item);
        }
        return hashSet;
    }
}
