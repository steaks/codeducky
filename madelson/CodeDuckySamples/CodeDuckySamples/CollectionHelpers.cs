namespace CodeDucky
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class CollectionHelpers
    {
        public static bool CollectionEquals<T>(this IEnumerable<T> @this, IEnumerable<T> that, IEqualityComparer<T> comparer = null)
        {
            // this is optional; if you want to be consistent with SequenceEqual, just throw exceptions if either argument is null instead
            if (@this == null) { return that == null; }
            else if (that == null) { return false; }

            var countedItems = @this.GroupBy(t => t, comparer).ToDictionary(
                g => g.Key,
                g => g.Count(),
                comparer);
            foreach (var item in that)
            {
                int count;
                if (!countedItems.TryGetValue(item, out count)) { return false; }
                if (count - 1 == 0) { countedItems.Remove(item); }
                else { countedItems[item] = count - 1; }
            }
            return countedItems.Count == 0;
        }

        /// <summary>
        /// If the given key is present in the dictionary, returns the value for that key. Otherwise, executes the given value factory
        /// function, adds the result to the dictionary, and returns that result
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> @this, TKey key, Func<TKey, TValue> valueFactory)
        {
            TValue value;
            if (!@this.TryGetValue(key, out value)) { @this.Add(key, value = valueFactory(key)); }
            return value;
        }
    }
}
