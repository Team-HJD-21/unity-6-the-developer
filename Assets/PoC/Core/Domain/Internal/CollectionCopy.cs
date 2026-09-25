// 모델 경계에서 목록을 방어적으로 복사하고 null 항목을 거부합니다.

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
