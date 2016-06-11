using System;
using System.Collections.Generic;
using System.Linq;
using static HaxlSharp.Internal.Base;
using HaxlSharp.Internal;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HaxlSharp.Test
{
    public class Post
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public int PostId { get; set; }
    }

    public class FetchPosts : Returns<IEnumerable<int>> { }

    public class FetchDuplicatePosts : Returns<ShowList<int>> { }

    public class GetPerson : Returns<Person>
    {
        public readonly int PersonId;
        public GetPerson(int personId)
        {
            PersonId = personId;
        }
    }

    public class GetNullPerson : Returns<Person> { }

    public class FetchPostInfo : Returns<PostInfo>
    {
        public readonly int PostId;
        public FetchPostInfo(int postId)
        {
            PostId = postId;
        }
    }

    public class GetTwoLatestPosts : Returns<Tuple<int, int>> { }

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

    public class Person
    {
        public int PersonId;
        public string Name;
        public IEnumerable<int> BestFriendIds;

        public override string ToString()
        {
            return $"Person {{ PersonId: {PersonId}, Name: {Name}, BestFriendIds: {BestFriendIds}  }}";
        }
    }



    public static class Blog
    {
        public static List<string> Names = new List<string>
        {
            "Cherry Greenburg",
            "Alison Herald",
            "Michal Zakrzewski",
            "Chance Kehoe",
            "Delaine Crago",
            "Sabina Barrs",
            "Peg Delosh",
            "Johnie Wengerd",
            "Shayne Knauer",
            "Tyson Dave",
            "Shandra Hanlin",
            "Rey Pita",
            "Jacquelyn Bivona",
            "Cristal Hornak",
            "Julieta Kilbane",
            "Terry Cavin",
            "Peppa Pig",
            "Charity Gadsden",
            "Antione Domingo",
            "Corazon Benito",
            "Tianna Bratton",
        };


        public static HaxlFetcher Fetcher()
        {
            return FetcherBuilder.New()

                .FetchRequest<FetchPosts, IEnumerable<int>>(_ =>
                {
                    return ShowList(Enumerable.Range(0, 10));
                })

                .FetchRequest<FetchDuplicatePosts, ShowList<int>>(_ =>
                {
                    return ShowList(Enumerable.Repeat(1, 10));
                })

                .FetchRequest<FetchPostInfo, PostInfo>(req =>
                {
                    var postId = req.PostId;
                    return new PostInfo(postId, DateTime.Today.AddDays(-postId), $"Topic {postId % 3}", (postId * 33) % 20);
                })

                .FetchRequest<FetchPostContent, string>(req =>
                {
                    return $"Post {req.PostId}";
                })

                .FetchRequest<FetchPostViews, int>(req =>
                {
                    return (req.PostId * 33) % 53;
                })

                .FetchRequest<GetNullPerson, Person>(async req =>
               {
                   await Task.Delay(10);
                   return null;
               })

                .FetchRequest<GetPerson, Person>(req =>
               {
                   var nameIndex = (req.PersonId * 33) % 20;
                   return new Person
                   {
                       Name = Names.ElementAt(nameIndex),
                       BestFriendIds = ShowList(new List<int>
                        {
                            (nameIndex + 3) % 20,
                            (nameIndex + 5) % 20,
                            (nameIndex + 7) % 20
                        }),
                       PersonId = req.PersonId
                   };

               })

                .FetchRequest<GetTwoLatestPosts, Tuple<int, int>>(req =>
                {
                    return new Tuple<int, int>(3, 4);
                })
                .LogWith(log => Debug.WriteLine(log.ToDefaultString()))
                .Create();
        }

        public static Fetch<Tuple<int, int>> FetchTwoLatestPosts()
        {
            return new GetTwoLatestPosts().ToFetch();
        }

        public static Fetch<ShowList<int>> FetchDuplicatePosts()
        {
            return new FetchDuplicatePosts().ToFetch();
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

        public static Fetch<ShowList<Person>> FetchPostAuthorFriends(int postId)
        {
            return from info in FetchPostInfo(postId)
                   from author in GetPerson(info.AuthorId)
                   from friends in author.BestFriendIds.SelectFetch(Blog.GetPerson)
                   select ShowList(friends);
        }

        public static Fetch<Person> FetchNullPerson()
        {
            return new GetNullPerson().ToFetch();
        }

        public static Fetch<PostDetails> GetPostDetails(int postId)
        {
            var x = from info in FetchPostInfo(postId)
                    from content in FetchPostContent(info.PostId)
                    select new PostDetails(info, content);
            return x;
        }

        public static Fetch<IEnumerable<PostInfo>> GetAllPostInfo()
        {
            return from postIds in FetchAllPostIds()
                   from postInfo in postIds.SelectFetch(FetchPostInfo)
                   select postInfo;
        }

        public static Fetch<Person> GetPerson(int personId)
        {
            return new GetPerson(personId).ToFetch();
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

    public class PostDetails
    {
        public PostInfo Info;
        public string Content;
        public PostDetails(PostInfo info, string content)
        {
            Info = info;
            Content = content;
        }

        public override string ToString()
        {
            return $"PostDetails {{ Info: {Info}, Content: '{Content}' }}";
        }
    }



}
