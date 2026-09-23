// Creates defensive list copies and rejects null object entries at model boundaries.

using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    internal static class CollectionCopy
    {
        public static List<T> CopyNonNull<T>(IEnumerable<T> source, string parameterName) where T : class
        {
            var copy = new List<T>();
            if (source == null) return copy;

            foreach (var item in source)
            {
                if (item == null) throw new ArgumentException("Collection entries cannot be null.", parameterName);
                copy.Add(item);
            }

            return copy;
        }
    }
}
