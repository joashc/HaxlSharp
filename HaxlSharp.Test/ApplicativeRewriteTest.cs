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
            var split = Splitter.Split(firstPostInfo);

            var result = await RunSplits.Run(split, Fetcher());
            ;
        }

        [TestMethod]
        public async Task Sequence_ShouldBeApplicative()
        {
            var getAllPostsInfo =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(Blog.FetchPostInfo)
                select postInfo;
            var split = Splitter.Split(getAllPostsInfo);
            var result = await RunSplits.Run(split, Fetcher());
        }


        [TestMethod]
        public void SharedDependency()
        {
            var fetch =
                from postIds in FetchAllPostIds()
                from postInfo in postIds.SelectFetch(Blog.FetchPostInfo)
                from firstPostInfo in FetchPostInfo(postIds.First())
                select firstPostInfo;
        }

        [TestMethod]
        public void LetNotation_Applicative()
        {
            var id = 0;
            var fetch = from postInfo in FetchPostInfo(id)
                        let id2 = 5
                        from postInfo2 in FetchPostInfo(id2)
                        select postInfo2;

        }

        [TestMethod]
        public void TwoLatestExample()
        {
            var fetch = from latest in FetchTwoLatestPosts()
                        from first in FetchPostInfo(latest.Item1)
                        from second in FetchPostInfo(latest.Item2)
                        from third in FetchPostInfo(2)
                        select first;
        }
    }
}
