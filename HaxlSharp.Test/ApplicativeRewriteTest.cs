using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using HaxlSharp.Internal;
using static HaxlSharp.Test.Blog;
using static HaxlSharp.Internal.Base;

namespace HaxlSharp.Test
{
    [TestClass]
    public class ApplicativeRewriteTest
    {
        public Fetcher fetcher = Fetcher();
        public HaxlCache cache = new HaxlCache(new HashedRequestKey());

        [TestInitialize]
        public void ClearCache()
        {
            cache = new HaxlCache(new HashedRequestKey());
        }

        [TestMethod]
        public async Task SingleFetch_ShouldHaveOneBatch()
        {
            var postIds = FetchAllPostIds();
            var fetch = postIds.ToHaxlFetch("result", Scope.New());
            var result = await RunFetch.Run(fetch, Scope.New(), fetcher.FetchBatch, cache);
        }

        [TestMethod]
        public async Task SelectFetch()
        {
            var fetch = FetchAllPostIds();
            var postIds = await fetch.FetchWith(fetcher, cache);
            var first = postIds.First();

            var fetch1 = from ids in FetchAllPostIds().Select(list => list.Select(x => x + 1))
                         from somethingElse in FetchAllPostIds()
                         select ids.First();
            var firstPlus1 = await fetch1.FetchWith(fetcher, cache);
            Assert.AreEqual(first + 1, firstPlus1);
        }

        [TestMethod]
        public async Task SelectFetchFinal()
        {
            var fetch = FetchAllPostIds();
            var postIds = await fetch.FetchWith(fetcher, cache);
            var first = postIds.First();

            cache = new HaxlCache(new HashedRequestKey());
            var fetch1 = from ids in FetchAllPostIds().Select(list => list.Select(x => x + 1))
                         select ids.First();
            var firstPlus1 = await fetch1.FetchWith(fetcher, cache);
            Assert.AreEqual(first + 1, firstPlus1);
        }

        [TestMethod]
        public async Task SelectLetFetch()
        {
            var fetch = FetchAllPostIds();
            var postIds = await fetch.FetchWith(fetcher, cache);
            var first = postIds.First();

            cache = new HaxlCache(new HashedRequestKey());
            var fetch1 = from ids in FetchAllPostIds().Select(list => list.Select(x => x + 1))
                         let first1 = ids.First()
                         from ids2 in FetchAllPostIds()
                         select first1;
            var firstPlus1 = await fetch1.FetchWith(fetcher, cache);
            Assert.AreEqual(first + 1, firstPlus1);
        }

        [TestMethod]
        public async Task SelectLetFetchExtended()
        {
            var fetch = FetchAllPostIds();
            var postIds = await fetch.FetchWith(fetcher, cache);
            var first = postIds.First();

            cache = new HaxlCache(new HashedRequestKey());

            var fetch1 = from ids in FetchAllPostIds().Select(list => list.Select(x => x + 1))
                         let first1 = ids.First()
                         from ids2 in FetchAllPostIds()
                         from ids3 in FetchAllPostIds()
                         select first1 + ids.First();
            var firstPlus1 = await fetch1.FetchWith(fetcher, cache);
            Assert.AreEqual(first + 1 + 1, firstPlus1);
        }

        [TestMethod]
        public async Task SequentialFetch_ShouldHaveTwoBatches()
        {
            var firstPostInfo = from postIds in FetchAllPostIds()
                                from firstInfo in FetchPostInfo(postIds.First())
                                select firstInfo;
            var result = await firstPostInfo.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task SequentialFetch_ShouldHaveTwoBatchesRepeat()
        {
            var firstPostInfo = from postIds in FetchAllPostIds()
                                from firstInfo in FetchPostInfo(postIds.Skip(1).First())
                                select firstInfo;
            var result = await firstPostInfo.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicative()
        {
            var getAllPostsInfo =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(GetPostDetails)
                select postInfo;
            var result = await getAllPostsInfo.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicativeAgain()
        {
            var getAllPostsInfo =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(GetPostDetails)
                select postInfo;
            var result = await getAllPostsInfo.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicativeAgainAddOne()
        {
            var getAllPostsInfo =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.Select(x => x + 1).SelectFetch(GetPostDetails)
                select postInfo;
            var result = await getAllPostsInfo.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task SharedDependency()
        {
            var fetch =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(Blog.FetchPostInfo)
                from firstPostInfo in FetchPostInfo(postIds.First())
                select firstPostInfo;
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task JustSequence()
        {
            var sequence = Enumerable.Range(0, 10).SelectFetch(Blog.FetchPostInfo);
            var result = await sequence.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task LetNotation_Applicative()
        {

            var id = 0;
            var fetch = from postInfo in FetchPostInfo(id)
                        let id2 = 1 + postInfo.PostId
                        from postInfo2 in FetchPostInfo(id2)
                        select postInfo2.PostTopic + id2;

            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task FinalLetNotation_Applicative()
        {

            var id = 0;
            var fetch = from postInfo in FetchPostInfo(id)
                        from postInfo2 in FetchPostInfo(1)
                        let id2 = 1 + postInfo.PostId
                        select postInfo2.PostTopic + id2;

            var result = await fetch.FetchWith(fetcher, cache);
            Assert.AreEqual("Topic 11", result);
        }

        [TestMethod]
        public async Task FinalLetNotation()
        {
            var id = 0;
            var fetch = from postInfo in FetchPostInfo(id)
                        let x = 3
                        from postInfo2 in FetchPostInfo(1)
                        let id2 = 1 + postInfo.PostId + 3
                        select postInfo2.PostTopic + id2 + x;

            var result = await fetch.FetchWith(fetcher, cache);
            Assert.AreEqual("Topic 143", result);
            
        }

        [TestMethod]
        public async Task FinalLetNotation_ApplicativeExtended()
        {

            var id = 0;
            var fetch = from postInfo in FetchPostInfo(id)
                        from postInfo2 in FetchPostInfo(1)
                        from postInfo3 in FetchPostInfo(postInfo2.PostId)
                        let id2 = 1 + postInfo.PostId + postInfo3.PostId
                        select postInfo2.PostTopic + id2;

            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task MultipleLetNotation_Applicative()
        {

            var id = 0;
            var let = FetchPostInfo(id).Select(info => info.PostId);
            var fetch = from postInfo in FetchPostInfo(id)
                        let id2 = 1 + postInfo.PostId
                        let id3 = 4
                        from postInfo2 in FetchPostInfo(id2)
                        select postInfo2.PostTopic + id2 + id3;

            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task TwoLatestExample()
        {
            var fetch = from latest in FetchTwoLatestPosts()
                        from first in GetPostDetails(latest.Item1)
                        from second in GetPostDetails(latest.Item2)
                        select new List<PostDetails> { first, second };
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task DuplicateNestedNames()
        {
            var nested = from x in FetchTwoLatestPosts()
                         from z in FetchTwoLatestPosts()
                         from y in GetPostDetails(x.Item1)
                         select $"[Nested: {y.Content}]";

            var nested2 = from x in nested
                          from y in nested
                          select $"[Nested2: [ {x}, {y} ]";
            var global = new { x = 3 };

            var fetch = from x in nested2
                        from y in nested2
                        from z in nested
                        let id2 = global.x
                        from n in nested
                        select $"Fetch: [ {x}, {y} ]";
            var result = await fetch.FetchWith(fetcher, cache);
            Debug.WriteLine(result);
            Assert.AreEqual(
                "Fetch: [ [Nested2: [ [Nested: Post 0], [Nested: Post 0] ], [Nested2: [ [Nested: Post 0], [Nested: Post 0] ] ]",
                result
            );

        }

        [TestMethod]
        public async Task NoNesting()
        {
            var fetch = from x in FetchTwoLatestPosts()
                        from y in FetchTwoLatestPosts()
                        from z in FetchTwoLatestPosts()
                        from n in FetchTwoLatestPosts()
                        select $"Fetch: [ {x}, {y} ]";
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task NestedLet()
        {
            var nested = from x in GetPostDetails(3)
                         from y in GetPostDetails(4)
                         select $"[ {x.Content}, {y.Content} ]";
            var fetch = from x in nested
                        let id = 3
                        from y in GetPostDetails(id)
                        select x + y.Content;
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task DuplicateNested()
        {
            var nested = from x in GetPostDetails(3)
                         from y in GetPostDetails(4)
                         select $"[ {x.Content}, {y.Content} ]";

            var nested2 = from x in nested
                          from z in nested
                          select $"{x}, {z}";

            var fetch = from x in nested2
                        from y in nested
                        select $"{x}, {y}";
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task TwoLatestExampleAgain()
        {
            var fetch = from latest in FetchTwoLatestPosts()
                        from first in GetPostDetails(latest.Item1 + 1)
                        from second in GetPostDetails(latest.Item2 + 2)
                        select new List<PostDetails> { first, second };
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task FetchDetails()
        {
            var fetch = GetPostDetails(1);
            var result = await fetch.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task FetchDetails2()
        {
            var fetch = from info in FetchPostInfo(2)
                        select new PostDetails(info, "Content");
            var result = await fetch.FetchWith(fetcher, cache);
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

        [TestMethod]
        public async Task Deduplication()
        {
            var fetch = from postIds in FetchDuplicatePosts()
                        from details in postIds.SelectFetch(GetPostDetails)
                        select ShowList(details);
            var result = await BlogFetch(fetch);
        }

        private Task<A> BlogFetch<A>(Fetch<A> request)
        {
            cache = new HaxlCache(new HashedRequestKey());
            return request.FetchWith(fetcher, cache);
        }

        [TestMethod]
        public async Task WithoutApplicative()
        {
            var latest = await BlogFetch(FetchTwoLatestPosts());
            var first = await BlogFetch(FetchPostInfo(latest.Item1));
            var firstContent = await BlogFetch(FetchPostContent(1));
            var second = await BlogFetch(FetchPostInfo(latest.Item2));
            var secondContent = await BlogFetch(FetchPostContent(latest.Item2));
        }
    }
}
