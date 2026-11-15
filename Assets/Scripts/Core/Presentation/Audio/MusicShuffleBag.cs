using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Presentation
{
    /// <summary>서로 다른 후보를 한 번씩 뽑고, 새 묶음의 첫 곡도 직전 곡과 다르게 합니다.</summary>
    public sealed class MusicShuffleBag<T> where T : class
    {
        private readonly T[] _items;
        private readonly int[] _order;
        private readonly Random _random;
        private int _next;
        private T _last;

        public MusicShuffleBag(IEnumerable<T> candidates, Random random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
            var unique = new List<T>(); var seen = new HashSet<T>();
            if (candidates != null) foreach (T value in candidates)
                if (value != null && seen.Add(value)) unique.Add(value);
            _items = unique.ToArray(); _order = new int[_items.Length]; _next = _items.Length;
        }
        public int Count => _items.Length;
        public T Next()
        {
            if (_items.Length == 0) return null;
            if (_next >= _items.Length)
            {
                for (int i = 0; i < _order.Length; i++) _order[i] = i;
                for (int i = _order.Length - 1; i > 0; i--)
                { int pick = _random.Next(i + 1); (_order[i], _order[pick]) = (_order[pick], _order[i]); }
                if (_items.Length > 1 && EqualityComparer<T>.Default.Equals(_items[_order[0]], _last))
                { int pick = 1 + _random.Next(_order.Length - 1); (_order[0], _order[pick]) = (_order[pick], _order[0]); }
                _next = 0;
            }
            return _last = _items[_order[_next++]];
        }
    }
}
