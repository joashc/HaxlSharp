using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using static HaxlSharp.Haxl;

namespace HaxlSharp.Test
{
    public class Post
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public int PostId { get; set; }
    }

    public class FetchPosts : Request<IEnumerable<int>>
    {
        IEnumerable<int> Request<IEnumerable<int>>.RunRequest()
        {
            return Enumerable.Range(0, 10);
        }
    }

    public class FetchPostInfo : Request<PostInfo>
    {
        private readonly int _postId;
        public FetchPostInfo(int postId)
        {
            _postId = postId;
        }

        public PostInfo RunRequest()
        {
            return new PostInfo(_postId, DateTime.Today.AddDays(-_postId), $"Topic {_postId % 3}");
        }
    }

    public class FetchPostContent : Request<string>
    {
        private readonly int _postId;
        public FetchPostContent(int postId)
        {
            _postId = postId;
        }

        public string RunRequest()
        {
            return $"Post {_postId}";
        }
    }

    public class FetchPostViews : Request<int>
    {
        private readonly int _postId;
        public FetchPostViews(int postId)
        {
            _postId = postId;
        }

        public int RunRequest()
        {
            return (_postId * 33) % 53;
        }
    }

    public static class Blog
    {
        public static Fetch<IEnumerable<int>> FetchPosts()
        {
            var fetcher = new MockFetcher();
            return new FetchPosts().DataFetch(fetcher);
        }

        public static Fetch<PostInfo> FetchPostInfo(int postId)
        {
            var fetcher = new MockFetcher();
            return new FetchPostInfo(postId).DataFetch(fetcher);
        }

        public static Fetch<string> FetchPostContent(int postId)
        {
            var fetcher = new MockFetcher();
            return new FetchPostContent(postId).DataFetch(fetcher);
        }

        public static Fetch<int> FetchPostViews(int postId)
        {
            var fetcher = new MockFetcher();
            return new FetchPostViews(postId).DataFetch(fetcher);
        }

        public static Task<Fetch<Tuple<PostInfo, string>>> GetPostDetails(int postId)
        {
            return Applicative(FetchPostInfo(postId), FetchPostContent(postId), (info, content) => new Tuple<PostInfo, string>(info, content));
        }
    }

    public class MockFetcher : Fetcher
    {
        public Task<A> AwaitResult<A>(Request<A> request)
        {
            return new Task<A>(() =>
            {
                Debug.WriteLine("Fetching");
                return request.RunRequest();
            });
        }
    }



    [TestClass]
    public class HaxlSharpTest
    {
        [TestMethod]
        public async Task QuerySyntax()
        {
            var getAllPostsInfo = 
                from postIds in Blog.FetchPosts()
                from postInfo in postIds.Select(Blog.FetchPostInfo).Sequence()
                select postInfo;


            var fetcher = new RunFetch<IEnumerable<PostInfo>>();
            var results = await getAllPostsInfo.Run(fetcher);

            foreach (var result in results)
            {
                Debug.WriteLine(result);
            }

            var detail = await Blog.GetPostDetails(0);
            var somethig = await detail.Fetch();

            Debug.WriteLine(somethig.Item1);
            Debug.WriteLine(somethig.Item2);


        }

        [TestMethod]
        public async Task Sequential()
        {
            var x = 0;
            await Task.WhenAll(Task.Delay(1000), Task.Delay(1000));
            x += 1;
            await Task.Delay(1000);
            x += 1;
            Assert.AreEqual(2, x);
        }
    }
}
