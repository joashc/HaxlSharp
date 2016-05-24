using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;

namespace HaxlSharp.Test
{
    public class TestFetcher<A> : Fetcher<A, Task<A>>
    {
        public int BatchCounter;
        public TestFetcher(int batchCounter)
        {
            BatchCounter = batchCounter;
        }

        public async Task<A> Blocked(Result<A> fetch, IEnumerable<Task> blockedRequests)
        {
            BatchCounter += 1;
            foreach (var blocked in blockedRequests)
            {
                blocked.Start();
            }
            await Task.WhenAll(blockedRequests);
            return await fetch.Run(this);
        }

        public Task<A> Done(Func<A> result)
        {
            return Task.Factory.StartNew(result);
        }
    }

    public static class TestFetchExt
    {
        public static async Task<int> CountBatches<A>(this Fetch<A> fetch)
        {
            var fetcher = new TestFetcher<A>(0);
            await fetch.Rewrite().Run(fetcher);
            return fetcher.BatchCounter;
        }

        public static Task<A> RunFetch<A>(this Result<A> result)
        {
            return result.Run(new TestFetcher<A>(0));
        }
    }

    [TestClass]
    public class ApplicativeRewriteTest
    {
        [TestMethod]
        public async Task SingleFetch_ShouldHaveOneBatch()
        {
            var postIds = Blog.FetchPosts();

            var batches = await postIds.CountBatches();

            Assert.AreEqual(1, batches);
        }

        [TestMethod]
        public async Task SequentialFetch_ShouldHaveTwoBatches()
        {
            var firstPostInfo = from postIds in Blog.FetchPosts()
                                from firstInfo in Blog.FetchPostInfo(postIds.First())
                                select firstInfo;

            var batches = await firstPostInfo.CountBatches();

            Assert.AreEqual(2, batches);
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicative()
        {
            var getAllPostsInfo =
                from postIds in Blog.FetchPosts()
                from postInfo in postIds.Select(Blog.FetchPostInfo).Sequence()
                select postInfo;

            var batches = await getAllPostsInfo.CountBatches();

            Assert.AreEqual(2, batches);
        }
    }
}
