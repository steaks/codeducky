using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CodeDucky.CustomInvokableBuilder;
using System.Threading;

namespace CodeDucky
{
    [TestClass]
    public class UnitTest1 : ICacheKey
    {
        //[TestMethod]
        //public void TestMethod1()
        //{
        //    CreateCustomInvokable.Run();
        //}

        [TestMethod]
        public void TestCache()
        {
            var cache = new Cache();
            var policy = new CachePolicy(TimeSpan.FromSeconds(5));
            cache.InvokeCached(() => this.Increment("a"), policy);
            Assert.AreEqual(1, cache.InvokeCached(() => this.Increment("a"), policy));
            Thread.Sleep(6000);
            Assert.AreEqual(2, cache.InvokeCached(() => this.Increment("a"), policy));
            Assert.AreEqual(3, cache.InvokeCached(() => this.Increment("b"), policy));
            Assert.AreEqual(3, cache.InvokeCached(() => this.Increment("b"), policy));
            var s = new string('b', 1);
            Assert.AreEqual(3, cache.InvokeCached(() => this.Increment(s), policy)); 
        }

        private int count;
        private int Increment(string arg)
        {
            return ++this.count;
        }

        void ICacheKey.BuildCacheKey(CacheKeyBuilder builder)
        {
            builder.By(this.GetType());
        }
    }
}
