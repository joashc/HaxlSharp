using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using static HaxlSharp.Test.Blog;

namespace HaxlSharp.Test
{

    [TestClass]
    public class ApplicativeRewriteTest
    {
        [TestMethod]
        public void SingleFetch_ShouldHaveOneBatch()
        {
            var postIds = FetchAllPostIds();
        }

        [TestMethod]
        public async Task SequentialFetch_ShouldHaveTwoBatches()
        {
            var firstPostInfo = from postIds in FetchAllPostIds()
                                from firstInfo in FetchPostInfo(postIds.First())
                                select firstInfo;
            var result = await firstPostInfo.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicative()
        {
            var getAllPostsInfo =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(Blog.GetPostDetails)
                select postInfo;
            var result = await getAllPostsInfo.FetchWith(Fetcher());
        }


        [TestMethod]
        public async Task SharedDependency()
        {
            var fetch =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(Blog.FetchPostInfo)
                from firstPostInfo in FetchPostInfo(postIds.First())
                select firstPostInfo;
            var split = fetch.Split();
            var collected = split.CollectRequests("sometning");
            var result = await fetch.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task JustSequence()
        {
            var sequence = Enumerable.Range(0, 10).SelectFetch(Blog.FetchPostInfo);
            var result = await sequence.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task LetNotation_Applicative()
        {
            var id = 0;
            var fetch = from postInfo in FetchPostInfo(id)
                        let id2 = 5
                        from postInfo2 in FetchPostInfo(id2)
                        select postInfo2;

            var result = await fetch.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task TwoLatestExample()
        {
            var fetch = from latest in FetchTwoLatestPosts()
                        from first in GetPostDetails(latest.Item1)
                        from second in GetPostDetails(latest.Item2)
                        select new List<PostDetails> { first, second };
            var result = await fetch.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task TwoLatestExampleAgain()
        {
            var fetch = from latest in FetchTwoLatestPosts()
                        from first in GetPostDetails(latest.Item1 + 1)
                        from second in GetPostDetails(latest.Item2 + 2)
                        select new List<PostDetails> { first, second };
            var result = await fetch.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task FetchDetails()
        {
            var fetch = GetPostDetails(1);
            var result = await fetch.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task TransparentAccess()
        {
            var fetch = from latest in FetchTwoLatestPosts()
                        from something in FetchPostInfo(1)
                        from somethingElse in FetchPostInfo(2)
                        from three in FetchPostInfo(3)
                        from newPost in FetchPostInfo(latest.Item1)
                        select newPost;
            var result = await BlogFetch(fetch);
        }

        private Task<A> BlogFetch<A>(Fetch<A> request)
        {
            return request.FetchWith(Fetcher());
        }

        [TestMethod]
        public async Task WithoutApplicative()
        {
            var latest = await BlogFetch(FetchTwoLatestPosts());
            var first = await BlogFetch(FetchPostInfo(latest.Item1));
            var firstContent = await BlogFetch(FetchPostContent(latest.Item1));
            var second = await BlogFetch(FetchPostInfo(latest.Item2));
            var secondContent = await BlogFetch(FetchPostContent(latest.Item2));
        }
    }
}
