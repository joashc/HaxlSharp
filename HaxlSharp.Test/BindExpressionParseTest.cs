using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Test
{
    [TestClass]
    public class BindExpressionParseTest
    {
        [TestMethod]
        public void ParseBindVar()
        {
            var blockNumber = GetBlockNumber("(0) int");
            Assert.AreEqual(0, blockNumber);
        }

        [TestMethod]
        public void ParseBigBindVar()
        {
            var blockNumber = GetBlockNumber("(7489230) int");
            Assert.AreEqual(7489230, blockNumber);
        }

        [TestMethod]
        public void InvalidParse()
        {
            try
            {
                var blockNumber = GetBlockNumber("((7489230)");
                Assert.Fail("No exception thrown.");
            }
            catch (ArgumentException)
            {
                // pass
            }
        }
    }
}
