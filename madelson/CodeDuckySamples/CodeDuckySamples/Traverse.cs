namespace CodeDucky
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class Traverse
    {
        /// <summary>
        /// Returns an <see cref="IEnumerable{T}"/> based on traversing the "linked list" represented by the head node
        /// and the next function. The list terminates when a null node is reached
        /// </summary>
        /// <typeparam name="T">the type of node in the list</typeparam>
        /// <param name="node">the head node of the list</param>
        /// <param name="next">a function that, given a node in the list, generates the next node in the list</param>
        public static IEnumerable<T> Along<T>(T node, Func<T, T> next)
            where T : class
        {
            Throw.IfNull(next, "next");

            for (var current = node; current != null; current = next(current))
            {
                yield return current;
            }
        }
    }
}
