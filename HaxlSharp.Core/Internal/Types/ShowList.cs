using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaxlSharp.Internal
{
    /// <summary>
    /// Simple pretty printing wrapper around IEnumerable.
    /// </summary>
    public class ShowList<A> : IEnumerable<A>
    {
        public readonly IEnumerable<A> List;
        public ShowList(IEnumerable<A> list)
        {
            List = list;
        }

        public IEnumerator<A> GetEnumerator()
        {
            return List.GetEnumerator();
        }

        public override string ToString()
        {
            if (!List.Any()) return "[]";

            var builder = new StringBuilder();
            builder.Append("[ ");
            var first = List.First();
            var rest = List.Skip(1);
            builder.Append(first);
            foreach (var item in rest)
            {
                builder.Append($", {item}");
            }
            builder.Append(" ]");
            return builder.ToString();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return List.GetEnumerator();
        }
    }
}
