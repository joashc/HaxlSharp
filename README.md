# HaxlSharp
A C# implementation of Haxl for composable, automatically concurrent data fetching.

## What's wrong with async/ await?
Sometimes we want to combine information from multiple data sources, like multiple remote APIs, or, as in this example, different calls on the same API.

Let's say we have a blogging site, and a post's metadata and content are retrieved using separate API calls. We could use async/ await to fetch both these pieces of information:

```cs
public Task<PostDetails> GetPostDetails(int postId)
{
    var postInfo = await FetchPostInfo(postId);
    var postContent = await FetchPostContent(postId);
    return new PostDetails(postInfo, postContent);
}
```

Here, we're making two successive `async` calls, which means the execution will be suspended at the first `await` while we fetch the data, and only resume once fetching is complete. The second `await` won't begin until the first has completed!

That's the problem with async/ await: even if a subsequent `await` doesn't depend on the result of a previous `await`, they're executed sequentially.

### Composing async methods
To make matters worse, we can easily call our inefficient `GetPostDetails` method from another method, compounding the oversequentialization:

```cs
public Task<IEnumerable<PostDetails> LatestPostContent()
{
  var latest = await GetTwoLatestPosts();
  var first = await GetPostDetails(latest.Item1);
  var second = await GetPostDetails(latest.Item2);
  return new List<PostContent>{first, second};
}
```

Here's what will happen if we execute this method:

- Wait for `GetTwoLatestPosts`
- Then wait for the first `GetPostDetails` call, which involves:
  - Waiting for `FetchPostInfo`
  - Then waiting for `FetchPostContent`
- Then wait for the second `GetPostDetails` call, again involving:
  - Waiting for `FetchPostInfo`
  - Then waiting for `FetchPostContent`

This code will sequentially execute four calls that could have been executed concurrently! We could manually deal with the concurrency by sprinkling our code with `Task.WhenAll`, but this is error-prone, obscures the meaning of the code, and relies on the programmer to reason about concurrency.

## A solution: automatic batching
This library allows us to write code that *looks* sequential, but is capable of being analyzed to determine the requestswe can fetch concurrently, and then automatically batch these requests together.

Let's rewrite `GetPostDetails` in this style:

```cs
public Fetch<PostDetails> GetPostDetails(int postId)
{
    return from postInfo in FetchPostInfo(postId);
           from postContent in FetchPostContent(postId);
           select new PostDetails(postInfo, postContent);
}
```

The framework can automatically work out that these calls can be parallelized. Here's what happens if we fetch `GetPostDetails(1)`:

```
=== Batch 1 ===
Fetched 'info': PostInfo { PostId: 1, PostDate: 1/06/2016, PostTopic: Topic 1}
Fetched 'content': Post 1

Result: PostDetails { PostInfo { PostId: 1, PostDate: 1/06/2016, PostTopic: Topic 1}, Post 1 }
```

Both of these were automatically placed in a single batch!

### Composing batches
Let's compose the call we just created:

```cs
public Fetch<List<PostDetails>> GetLatestPostDetails()
{
  return from latest in FetchTwoLatestPosts()
         // We must wait here
         from first in GetPostDetails(latest.Item1)
         from second in GetPostDetails(latest.Item2)
         select new List<PostDetails> { first, second };
}
```

If we fetch this, we get:

```
=== Batch 1 ===
Fetched 'latest': (0, 1)

=== Batch 2 ===
Fetched 'info': PostInfo { PostId: 0, PostDate: 2/06/2016, PostTopic: Topic 0}
Fetched 'content': Post 0
Result: PostDetails { PostInfo { PostId: 0, PostDate: 2/06/2016, PostTopic: Topic 0}, Post 0 }

Fetched 'info': PostInfo { PostId: 1, PostDate: 1/06/2016, PostTopic: Topic 1}
Fetched 'content': Post 1
Result: PostDetails { PostInfo { PostId: 1, PostDate: 1/06/2016, PostTopic: Topic 1}, Post 1 }

Result: System.Collections.Generic.List`1[HaxlSharp.Test.PostDetails]
```

The framework has worked out that we have no choice but to wait for the first call's result, because we rely on this result to execute our subsequent calls. But because these subsequent calls only depend on `latest`, as soon as `latest` is fetched, they can both be fetched concurrently!

## Usage
I'm in the process of completely rewriting this library, so details here are bound to change. I'm undecided on the API for this library; currently it's similar to ServiceStack's.

### Defining requests
You can define requests with POCOs; just annotate their return type like so:

```cs
public class FetchPostInfo : Returns<PostInfo>
{
    public readonly int PostId;
    public FetchPostInfo(int postId)
    {
        PostId = postId;
    }
}
```

### Using the requests
We need to convert these requests from a `Returns<>` into a `Fetch<>` if we want to get our concurrent fetching and composability: 

```cs
Fetch<PostInfo> fetchInfo = new FetchPostInfo(2).ToFetch();
```

It's a bit cumbersome to `new` up a request object every time we want to make a request, especially if we're going to be composing them. So we can write a method that returns a `Fetch<>` for every request:

```cs
public static Fetch<PostInfo> GetPostInfo(int postId)
{
  return new FetchPostInfo(postId).ToFetch();
}
```

Now we can compose any function that returns a `Fetch<>`, and they'll be automatically batched as much as possible:

```cs
public static Fetch<PostDetails> GetPostDetails(int postId) 
{
    return from info in GetPostInfo(postId)
           from content in GetPostContent(postId)
           select new PostDetails(info, content);
}

public static Fetch<IEnumerable<PostDetails>> RecentPostDetails() 
{
    return from postIds in GetAllPostIds()
           from postDetails in postIds.Take(10).SelectFetch(getPostDetails)
           select postDetails;
}
```

### Handling requests
Of course, the data must come from somewhere, so we must create handlers for every request type. Handlers are just functions from the request type to the response type. Register these functions to create a `Fetcher` object:

```cs
var fetcher = FetcherBuilder.New()

.FetchRequest<FetchPosts, IEnumerable<int>>(_ =>
{
    return _postApi.GetAllPostIds();
})

.FetchRequest<FetchPostInfo, PostInfo>(req =>
{
    return _postApi.GetPostInfo(req.PostId);
})

.FetchRequest<FetchUser, User>(req => {
    return _userApi.GetUser(req.UserId);
})

.Create();
```

This object can be injected wherever you want to resolve a `Fetch<A>` into an `A`:

```cs
Fetch<IEnumerable<string>> getPopularContent =
    from postIds in GetAllPostIds()
    from views in postIds.SelectFetch(GetPostViews)
    from popularPosts in views.OrderByDescending(v => v.Views)
                             .Take(5)
                             .SelectFetch(v => GetPostContent(v.Id))
    select popularPosts;

IEnumerable<string> popularContent = await fetcher.Fetch(getPopularContent);
```

Ideally, we should work within the `Fetch<>` monad as much as possible, and only resolve the final `Fetch<>` when absolutely necessary. This ensures the framework performs the fetches in the most efficient way.

### Caching
Caching isn't implemented yet, but implementing the Fetcher interface allows us to customize the fetching behaviour, including any caching. We can atomically cache responses per request, so that multiple calls to the same endpoint will give the same result, which is useful for capturing a consistent "snapshot" of data.

### Deduplication
Because our fetcher is handed a list of requests per batch, we can perform deduplication on the request list, and avoid unnecessary overhead. Simple per-request caching is insufficient, as duplicate requests that are part of the same batch will be started concurrently.
