using System.Collections.Generic;

namespace MVNet
{
    static internal class IListExtensions
    {
        public static void Add(this IList<KeyValuePair<string, string>> list, string key, object value)
            => list.Add(new KeyValuePair<string, string>(key, value.ToString()));
    }
}
