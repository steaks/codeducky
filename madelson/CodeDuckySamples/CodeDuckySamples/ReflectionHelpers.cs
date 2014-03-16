namespace CodeDucky
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public static class ReflectionHelpers
    {
        /// <summary>
        /// Gets generic arguments from the given type for the given type definition
        /// </summary>
        public static Type[] GetGenericArguments(this Type @this, Type genericTypeDefinition)
        {
            Throw.IfNull(@this, "this");
            Throw.IfNull(genericTypeDefinition, "genericTypeDefinition");
            Throw.If(!genericTypeDefinition.IsGenericTypeDefinition, "genericTypeDefinition: must be a generic type definition");

            if (genericTypeDefinition.IsInterface)
            {
                var @interface = @this.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDefinition);
                return @interface.NullSafe(i => i.GetGenericArguments(), Type.EmptyTypes);
            }
            if (@this.IsGenericType && @this.GetGenericTypeDefinition() == genericTypeDefinition)
            {
                return @this.GetGenericArguments();
            }
            return @this.BaseType.NullSafe(t => t.GetGenericArguments(genericTypeDefinition), Type.EmptyTypes);
        }

        /// <summary>
        /// Returns the method referenced by the given expression
        /// </summary>
        public static MethodInfo GetMethod<TInstance>(Expression<Action<TInstance>> methodExpression)
        {
            Throw.IfNull(methodExpression, "methodExpression");
            var methodCall = (MethodCallExpression)methodExpression.Body;
            return methodCall.Method;
        }

        public static FieldInfo GetField<TField>(Expression<Func<TField>> memberExpression)
        {
            return (FieldInfo)GetMember<TField>(memberExpression);
        }

        public static MemberInfo GetMember<TResult>(Expression<Func<TResult>> memberExpression)
        {
            return ((MemberExpression)memberExpression.Body).Member;
        }
    }
}
