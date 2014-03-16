using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using CodeDucky.CustomInvokableBuilder;

namespace CodeDucky
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            CreateCustomInvokable.Run();
        }
    }
}
