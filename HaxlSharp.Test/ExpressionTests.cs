using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Test
{
    public class Nested
    {
        public int x { get { return 99; } }
    }

    [TestClass]
    public class ExpressionTests
    {
        public const int global = 2;
        public static Identity<int> a = new Identity<int>(global);
        public static Identity<int> b = new Identity<int>(3);
        public static Func<int, Identity<int>> c = i => new Identity<int>(i);
        public static Func<int, int, Identity<int>> c2 = (i, i2) => new Identity<int>(i);
        public static Func<int, Identity<int>> d = i => new Identity<int>(i);
        public static Nested nested = new Nested();

        public static int CountAt<A>(SplitApplicatives<A> split, int i)
        {
            return split.Segments.ElementAt(i).Expressions.Count();
        }

        [TestMethod]
        public void ExpressionTest()
        {
            var nested = from xa in new Identity<int>(66)
                             // split
                         from za in c(xa)
                         from ya in b
                         select xa + ya + za;

            var expression = from x in nested
                                 //split
                             from z in c(x)
                             from y in b
                                 // split
                             from w in d(y)
                             select x + y + z + w;
            var split = Splitter.Split(expression);
            Assert.AreEqual(true, split.FirstSplit);
            Assert.AreEqual(4, split.Segments.Count());
            Assert.AreEqual(1, CountAt(split, 0));
            Assert.AreEqual(3, CountAt(split, 1));
            Assert.AreEqual(2, CountAt(split, 2));
            Assert.AreEqual(2, CountAt(split, 3));
        }

        [TestMethod]
        public void ExpressionTest2()
        {
            var expression = from x in a
                             from y in new Identity<int>(nested.x)
                                 // split
                             from z in c2(x, y)
                             select x + y + z;
            var split = Splitter.Split(expression);
            Assert.AreEqual(false, split.FirstSplit);
            Assert.AreEqual(2, split.Segments.Count());
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest3()
        {
            var expression = from x in a
                             // split
                             from y in c(x)
                             from z in c(x)
                             select x + y + z;
            var split = Splitter.Split(expression);
            Assert.AreEqual(true, split.FirstSplit);
            Assert.AreEqual(2, split.Segments.Count());
            Assert.AreEqual(1, CountAt(split, 0));
            Assert.AreEqual(3, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest4()
        {
            var expression = from f in a
                             from x in a
                             // split
                             from y in c(x)
                             from z in c(nested.x)
                             select x + y + z;
            var split = Splitter.Split(expression);
            Assert.AreEqual(false, split.FirstSplit);
            Assert.AreEqual(2, split.Segments.Count());
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(3, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest5()
        {
            var expression = from x in a
                             from z in c(nested.x)
                             // split
                             from y in c(x + 3)
                             select x + y + z;
            var split = Splitter.Split(expression);
            Assert.AreEqual(false, split.FirstSplit);
            Assert.AreEqual(2, split.Segments.Count());
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest6()
        {
            var expression = from z in c(nested.x)
                             from x in a
                             // split
                             from y in c(x + 3)
                             select x + y + z;
            var split = Splitter.Split(expression);
            Assert.AreEqual(false, split.FirstSplit);
            Assert.AreEqual(2, split.Segments.Count());
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));

        }

        [TestMethod]
        public void LetTest()
        {
            //var memberType = DetectApplicative.GetTransMemberType(node);
            //var dictionary = Expression.Constant(boundVariables);
            //var result = Expression.Property(dictionary, "Item", Expression.Constant(node.Member.Name));
            //var key = Expression.Parameter(typeof(string), "key");
            //var expression = from x in a
            //                 let q = x + 3
            //                 from z in c(x)
            //                 from y in c(x + 3)
            //                 select x + y + z;
            //var split = SplitApplicative.Split(expression);
            //Assert.AreEqual(true, split.FirstSplit);
            //Assert.AreEqual(1, split.Segments.Count());
        }

        [TestMethod]
        public void Rewrite()
        {
            var expression = from x in a
                             from y in b
                             //split
                             from z in c(x)
                             from w in d(y)
                             select x + y + z + w;
            var split = Splitter.Split(expression);
        }

        [TestMethod]
        public void SequenceRewrite()
        {
            var list = Enumerable.Range(0, 10);
            Func<int, Identity<int>> mult10 = x => new Identity<int>(x * 10);
            var expression = from x in new Identity<IEnumerable<int>>(list)
                             from multiplied in x.Select(mult10).Sequence()
                             from added in x.Select(num => new Identity<int>(num + 1)).Sequence()
                             select added.Concat(multiplied);

            var split = Splitter.Split(expression);
            ;
        }
    }
}
