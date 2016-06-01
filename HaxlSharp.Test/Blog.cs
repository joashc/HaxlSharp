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

    public class FetchPosts : Returns<IEnumerable<int>>
    {
    }

    public class FetchPostInfo : Returns<PostInfo>
    {
        public readonly int PostId;
        public FetchPostInfo(int postId)
        {
            PostId = postId;
        }
    }

    public class GetTwoLatestPosts : Returns<Tuple<int, int>>
    {
    }

    public class FetchPostContent : Returns<string>
    {
        public readonly int PostId;
        public FetchPostContent(int postId)
        {
            PostId = postId;
        }
    }

    public class FetchPostViews : Returns<int>
    {
        public readonly int PostId;
        public FetchPostViews(int postId)
        {
            PostId = postId;
        }
    }


    public static class Blog
    {
        public static Fetcher Fetcher()
        {
            return FetcherBuilder.New()

                .FetchRequest<FetchPosts, IEnumerable<int>>(_ =>
                {
                    return Enumerable.Range(0, 30);
                })

                .FetchRequest<FetchPostInfo, PostInfo>(req =>
                {
                    var postId = req.PostId;
                    return new PostInfo(postId, DateTime.Today.AddDays(-postId), $"Topic {postId % 3}");
                })

                .FetchRequest<FetchPostContent, string>(req =>
                {
                    return $"Post {req.PostId}";
                })

                .FetchRequest<FetchPostViews, int>(req =>
                {
                    return (req.PostId * 33) % 53;
                })

                .FetchRequest<GetTwoLatestPosts, Tuple<int, int>>(req =>
                {
                    return new Tuple<int, int>(0, 1);
                })

                .Create();
        }

        public static Fetch<Tuple<int, int>> FetchTwoLatestPosts()
        {
            return new GetTwoLatestPosts().ToFetch();
        }

        public static Fetch<IEnumerable<int>> FetchAllPostIds()
        {
            return new FetchPosts().ToFetch();
        }

        public static Fetch<PostInfo> FetchPostInfo(int postId)
        {
            return new FetchPostInfo(postId).ToFetch();
        }

        public static Fetch<string> FetchPostContent(int postId)
        {
            return new FetchPostContent(postId).ToFetch();
        }

        public static Fetch<int> GetFirstPostId()
        {
            return from posts in GetAllPostInfo()
                   select posts.OrderByDescending(p => p.PostDate).First().PostId;
        }

        public static Fetch<int> FetchPostViews(int postId)
        {
            return new FetchPostViews(postId).ToFetch();
        }

        public static Fetch<Tuple<PostInfo, string>> GetPostDetails(int postId)
        {
            var x = from info in FetchPostInfo(postId)
                    from content in FetchPostContent(postId)
                    select new Tuple<PostInfo, string>(info, content);
            return x;
        }

        public static Fetch<IEnumerable<PostInfo>> GetAllPostInfo()
        {
            return from postIds in FetchAllPostIds()
                   from postInfo in postIds.SelectFetch(FetchPostInfo)
                   select postInfo;
        }

        public static Fetch<IEnumerable<string>> RecentPostContent()
        {
            return from posts in GetAllPostInfo()
                   from recentContent in
                        posts.OrderByDescending(p => p.PostDate)
                             .Take(4)
                             .SelectFetch(pi => FetchPostContent(pi.PostId))
                   select recentContent;
        }

    }

}
