namespace CodeDucky
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class Helpers
    {
        /// <summary>
        /// Bounds a value within a range
        /// </summary>
        public static T Capped<T>(this T @this, T? min = null, T? max = null)
            where T : struct, IComparable<T>
        {
            return min.HasValue && @this.CompareTo(min.Value) < 0 ? min.Value
                : max.HasValue && @this.CompareTo(max.Value) > 0 ? max.Value
                : @this;
        }

        /// <summary>
        /// Type-safely casts the given value to the specified type
        /// </summary>
        public static T As<T>(this T @this) 
        { 
            return @this; 
        }

        /// <summary>
        /// Invokes the given function on the given object if and only if the given object is not null. Otherwise,
        /// the value specified by "ifNullReturn" is returned
        /// </summary>
        public static TResult NullSafe<TObj, TResult>(
            this TObj obj,
            Func<TObj, TResult> func,
            TResult ifNullReturn = default(TResult))
        {
            Throw.IfNull(func, "func");
            return obj != null ? func(obj) : ifNullReturn;
        }
    }
}
