namespace CodeDucky
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;

    public static class Enums
    {
        public static IEnumerable<TEnum> Values<TEnum>()
            where TEnum : struct
        {
            return Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
        }

        private class EnumHelper<TEnum>
        {
            public static readonly Func<TEnum, TEnum, bool> HasFlag;
            public static TEnum[] Values;

            static EnumHelper()
            {
                var underlyingType = typeof(TEnum).GetEnumUnderlyingType();
                var thisParam = Expression.Parameter(typeof(TEnum));
                var flagParam = Expression.Parameter(typeof(TEnum));
                var lambda = Expression.Lambda<Func<TEnum, TEnum, bool>>(
                    Expression.Equal(
                        Expression.And(
                            Expression.Convert(thisParam, underlyingType),
                            Expression.Convert(flagParam, underlyingType)
                        ),
                        Expression.Convert(flagParam, underlyingType)
                    ),
                    thisParam,
                    flagParam
                );
                HasFlag = lambda.Compile();

                Values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>().ToArray();
            }
        }

        public static bool HasFlag2<TEnum>(this TEnum @this, TEnum flag)
            where TEnum : struct
        {
            return EnumHelper<TEnum>.HasFlag(@this, flag);
        }

        public static bool IsDefined2<TEnum>(this TEnum @this)
        {
            return EnumHelper<TEnum>.Values.Contains(@this);
        }

        public static TAttribute GetAttribute<TAttribute>(this Enum @this)
            where TAttribute : Attribute
        {
            return @this.GetType().GetField(@this.ToString(), BindingFlags.Public | BindingFlags.Static)
                .GetCustomAttribute<TAttribute>();
        }
    }

    [TestClass]
    public class EnumTests
    {
        [Flags]
        public enum F
        {
            A = 1,
            B = 2,
            C = 4
        }

        public enum A
        {
            [System.ComponentModel.Description("X is cool")]
            X, 
            Y, 
            Z
        }

        [TestMethod]
        public void HasFlagTest()
        {
            Perf.Test(() => (F.A | F.B).HasFlag(F.B));
            Perf.Test(() => (F.A | F.B).HasFlag2(F.B));
            throw new Exception("fail");
        }

        [TestMethod]
        public void IsDefinedTest()
        {
            var wrong = (A)(100);
            Perf.Test(() => Enum.IsDefined(typeof(A), wrong));
            Perf.Test(() => wrong.IsDefined2());
            throw new Exception("fail");
        }

        [TestMethod]
        public void TestGetName()
        {
            Perf.Test(() => typeof(A).GetEnumName(A.Y));
            Perf.Test(() => Enum.GetName(typeof(A), A.Y));
            Perf.Test(() => A.Y.ToString());
            throw new Exception("fal");
            
        }

        [TestMethod]
        public void TestGetUnderlyingType()
        {
            Perf.Test(() => Enum.GetUnderlyingType(typeof(A)));
            Perf.Test(() => typeof(A).GetEnumUnderlyingType());
            throw new Exception("fal");
        }

        [TestMethod]
        public void TestAttr()
        {
            Assert.AreEqual("X is cool", A.X.GetAttribute<System.ComponentModel.DescriptionAttribute>().Description);
        }

        [TestMethod]
        public void TestIs()
        {
            Assert.AreEqual(typeof(Enum), typeof(A).BaseType);
            Assert.IsTrue(A.X is Enum);
        }
    }
}
