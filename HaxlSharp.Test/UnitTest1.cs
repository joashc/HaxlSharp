using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using static HaxlSharp.Haxl;
using System.Linq.Expressions;

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

        public static Fetch<Tuple<PostInfo, string>> GetPostDetails(int postId)
        {
            var x = from info in FetchPostInfo(postId)
                    from content in FetchPostContent(postId)
                    select new Tuple<PostInfo, string>(info, content);
            return x;
        }
    }

    public class MockFetcher : Fetcher
    {
        public Task<A> AwaitResult<A>(Request<A> request)
        {
            return new Task<A>(() =>
            {
                var result = request.RunRequest();
                Debug.WriteLine($"Fetched: {result}");
                return result;
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

            await getAllPostsInfo.Rewrite().RunFetch();
        }

    }
}
