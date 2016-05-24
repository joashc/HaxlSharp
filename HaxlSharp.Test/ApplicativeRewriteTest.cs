using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace HaxlSharp.Test
{
    public static class ApplicativeRewriteTestExt
    {
        public async static Task<int> BatchCount<A>(this Fetch<A> fetch)
        {
            var batchCount = 0;
            BatchEvents.BatchOccurredEvent += (_, __) => batchCount += 1;
            await fetch.Rewrite().RunFetch();
            return batchCount;
        }
    }

    [TestClass]
    public class ApplicativeRewriteTest
    {
        [TestMethod]
        public async Task SingleFetch_ShouldHaveOneBatch()
        {
            var postIds = Blog.FetchPosts();
            var batchCount = await postIds.BatchCount();

            Assert.AreEqual(1, batchCount);
        }

        [TestMethod]
        public async Task SequentialFetch_ShouldHaveTwoBatches()
        {
            var firstPostInfo = from postIds in Blog.FetchPosts()
                                from firstInfo in Blog.FetchPostInfo(postIds.First())
                                select firstInfo;

            var batchCount = await firstPostInfo.BatchCount();

            Assert.AreEqual(2, batchCount);
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicative()
        {
            var getAllPostsInfo =
                from postIds in Blog.FetchPosts()
                from postInfo in postIds.Select(Blog.FetchPostInfo).Sequence()
                select postInfo;

            var batchCount = await getAllPostsInfo.BatchCount();

            Assert.AreEqual(2, batchCount);
        }

        [TestMethod]
        public async Task SharedDependency()
        {
            var fetch =
                from postIds in Blog.FetchPosts()
                from postInfo in postIds.Select(Blog.FetchPostInfo).Sequence()
                from firstPostInfo in Blog.FetchPostInfo(postIds.First())
                select firstPostInfo;

            var batchCount = await fetch.BatchCount();

            Assert.AreEqual(2, batchCount);
        }

        [TestMethod]
        public async Task LetNotation_Applicative()
        {
            var id = 0;
            var fetch = from postInfo in Blog.FetchPostInfo(id)
                        let id2 = 5
                        from postInfo2 in Blog.FetchPostInfo(id2)
                        select postInfo2;

            var batchCount = await fetch.BatchCount();

            Assert.AreEqual(1, batchCount);
        }

        [TestMethod]
        public async Task TransformList()
        {
            var batchCount = await Blog.RecentPostContent().BatchCount();

            Assert.AreEqual(3, batchCount);

        }
    }
}
