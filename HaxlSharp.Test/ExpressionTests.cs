using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using HaxlSharp.Internal;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Test
{
    public class Nested
    {
        public int x { get { return 99; } }
    }

    public class TestFetch<A> : FetchNode<A>
    {
        public readonly A Value;

        public TestFetch(A val)
        {
            Value = val;
        }

        public override Haxl ToHaxlFetch(string bindTo, Scope scope)
        {
            return Haxl.Pure(bindTo, Value);
        }
    }


    [TestClass]
    public class ExpressionSplitTests
    {
        public const int global = 2;
        public static TestFetch<int> a = new TestFetch<int>(global);
        public static TestFetch<int> b = new TestFetch<int>(3);
        public static Func<int, TestFetch<int>> c = i => new TestFetch<int>(i);
        public static Func<int, int, TestFetch<int>> c2 = (i, i2) => new TestFetch<int>(i);
        public static Func<int, TestFetch<int>> d = i => new TestFetch<int>(i);
        public static Nested nested = new Nested();

        public static int CountAt(BindSegments split, int i)
        {
            if (split is BindSegments) return ( split).Segments.ElementAt(i).BoundExpressions.Count();
            return 0;
        }

        public static int SplitCount(BindSegments split)
        {
            return split.Segments.Where(s => !s.IsProjectGroup).Count();
        }

        public static int ProjectCount(BindSegments split)
        {
            return split.Segments.Where(s => s.IsProjectGroup).Count();
        }

        public static BindSegments Split<A>(Fetch<A> fetch)
        {
            var type = fetch.GetType();
            Assert.IsTrue(type.GetGenericTypeDefinition() == typeof(Bind<,,>));
            return QuerySplitter.Bind(fetch.CollectedExpressions, fetch.Initial);
        }

        [TestMethod]
        public void DuplicateVariableNames()
        {
            // We've got two variables of different type named 'x'.

            var nested =     from x in new TestFetch<string>("1")           // Group 0.1
                             // split                                       // =========
                             from za in c(int.Parse(x))                     // Group 1.1
                             from ya in b                                   // Group 1.2
                             //projection                                   // =========
                             select ya;                                     // Group 2.1 (Projection)
            var expression = from x in nested                               // 
                             // split                                 		// =========
                             from z in c(x)                           		// Group 3.1
                             from y in b                              		// Group 3.2
                             // split                                 		// =========
                             from w in d(y)                           		// Group 4.1
                             // projection                            		// =========
                             select x + y + z + w;                    		// Group 5.1 (Projection)

            var split = Split(expression);
            Assert.AreEqual(4, SplitCount(split));
            Assert.AreEqual(2, ProjectCount(split));
            Assert.AreEqual(1, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));
            Assert.AreEqual(1, CountAt(split, 2)); 
            Assert.AreEqual(2, CountAt(split, 3));
            Assert.AreEqual(1, CountAt(split, 4));
        }

        [TestMethod]
        public void ExpressionTest()
        {
            var nested =     from xa in new TestFetch<int>(66)        // Group 0.1
                             // split                                 // =========
                             from za in c(xa)                         // Group 1.1
                             from ya in b                             // Group 1.2
                             //projection                             // =========
                             select xa + ya + za;                     // Group 2.1 (Projection)
            var expression = from x in nested                         // 
                             // split                                 // =========
                             from z in c(x)                           // Group 3.1
                             from y in b                              // Group 3.2
                             // split                                 // =========
                             from w in d(y)                           // Group 4.1
                             // projection                            // =========
                             select x + y + z + w;                    // Group 5.1 (Projection)

            var split = Split(expression);
            Assert.AreEqual(4, SplitCount(split));
            Assert.AreEqual(2, ProjectCount(split));
            Assert.AreEqual(1, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));
            Assert.AreEqual(1, CountAt(split, 2)); 
            Assert.AreEqual(2, CountAt(split, 3));
            Assert.AreEqual(1, CountAt(split, 4));
        }

        [TestMethod]
        public void ExpressionTest2()
        {
            var expression = from x in a
                             from y in new TestFetch<int>(nested.x)
                                 // split
                             from z in c2(x, y)
                             select x + y + z;
            var split = Split(expression);
            Assert.AreEqual(2, SplitCount(split));
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(1, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest3()
        {
            var expression = from x in a
                                 // split
                             from y in c(x)
                             from z in c(x)
                             select x + y + z;
            var split = Split(expression);
            Assert.AreEqual(2, SplitCount(split));
            Assert.AreEqual(1, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));
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
            var split = Split(expression);
            Assert.AreEqual(2, SplitCount(split));
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(2, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest5()
        {
            var expression = from x in a
                             from z in c(nested.x)
                                 // split
                             from y in c(x + 3)
                             select x + y + z;
            var split = Split(expression);
            Assert.AreEqual(2, SplitCount(split));
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(1, CountAt(split, 1));
        }

        [TestMethod]
        public void ExpressionTest6()
        {
            var expression = from z in c(nested.x)
                             from x in a
                                 // split
                             from y in c(x + 3)
                             select x + y + z;
            var split = Split(expression);
            Assert.AreEqual(2, SplitCount(split));
            Assert.AreEqual(2, CountAt(split, 0));
            Assert.AreEqual(1, CountAt(split, 1));

        }

        [TestMethod]
        public void LetTest()
        {
            var expression = from x in a
                             let q = x + 3
                             from z in c(q)
                             from y in c(q + 3)
                             select x + y + z;
            var split = Split(expression);
        }

        [TestMethod]
        public void NestedQuery()
        {
            var expression = from x in (from x in a select x + 1) 
                             from y in b
                                 //split
                             from z in c(x)
                             from w in d(y)
                             select x + y + z + w;
            var split = Split(expression);

        }

        [TestMethod]
        public void SequenceRewrite()
        {
            var list = Enumerable.Range(0, 10);
            Func<int, TestFetch<int>> mult10 = x => new TestFetch<int>(x * 10);
            var expression = from x in new TestFetch<IEnumerable<int>>(list)
                             from multiplied in x.SelectFetch(mult10)
                             from added in x.SelectFetch(num => new TestFetch<int>(num + 1))
                             select added.Concat(multiplied);

            var split = Split(expression);
        }


        [TestMethod]
        public void SequenceRewriteConcurrent()
        {
            var list = Enumerable.Range(0, 1000);
            Func<int, TestFetch<int>> mult10 = x => new TestFetch<int>(x * 10);
            var expression = from x in new TestFetch<IEnumerable<int>>(list)
                             from multiplied in x.SelectFetch(mult10)
                             from added in x.SelectFetch(num => new TestFetch<int>(num))
                             select added.Concat(multiplied);

            var split = Split(expression);
        }

        [TestMethod]
        public void OneLiner()
        {
            var oneLine = from x in new TestFetch<int>(3)
                          select x + 1;
        }

        [TestMethod]
        public void SelectTest()
        {
            var number = new TestFetch<int>(3);
            var plusOne = number.Select(num => num + 1);
            var plusTwo = plusOne.Select(num => num + 1);
        }
    }
}
