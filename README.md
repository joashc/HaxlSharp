# HaxlSharp
A C# implementation of [Haxl](https://github.com/facebook/Haxl) for composable data fetching with automatic concurrency and request deduplication. Not affiliated with Facebook in any way!

Table of Contents
=================

  * [Quick start](#quick-start)
  * [What's wrong with async/ await?](#whats-wrong-with-async-await)
    * [Composing async methods](#composing-async-methods)
    * [What's wrong with Task.WhenAll/ Promise.all?](#whats-wrong-with-taskwhenall-promiseall)
  * [Haxl: reclaiming the sequential abstraction](#haxl-reclaiming-the-sequential-abstraction)
    * [Composing requests](#composing-requests)
    * [Request deduplication](#request-deduplication)
  * [Implementation details](#implementation-details)
  * [Integration](#integration)
    * [Defining requests](#defining-requests)
    * [Using the requests](#using-the-requests)
    * [Handling requests](#handling-requests)
    * [Implementing your own fetcher](#implementing-your-own-fetcher)
      * [Why would you want to implement your own fetcher/ caching strategy?](#why-would-you-want-to-implement-your-own-fetcher-caching-strategy)
      * [Fetcher](#fetcher)
      * [Caching](#caching)
  * [Limitations](#limitations)
    * [Speed](#speed)
    * [Anonymous types](#anonymous-types)
    * [Applicative Do](#applicative-do)
    * [It's a giant hack](#its-a-giant-hack)

## Quick start 
Install from nuget: [https://www.nuget.org/packages/HaxlSharp](https://www.nuget.org/packages/HaxlSharp)

Before you can use the library, you'll need to write a thin layer to get your existing data sources integrated with HaxlSharp- see the [Integration](#integration) section, or you can check out an example application using HaxlFetch [here](https://github.com/joashc/HaxlSharpDemo).

Once that's done, you can write your data fetches in a sequential way, and the framework will automatically perform requests as concurrently as possible, and do request deduplication.

## What's wrong with async/ await?
Async/ await is great for writing sequential-looking code when you're only waiting for a single asynchronous request at a time. But we often want to combine information from multiple data sources, like different calls on the same API, or multiple remote APIs.

The async/ await abstraction breaks down in these situations (and Javascript's async/await is no different). To illustrate, let's say we have a blogging site, and a post's metadata and content are retrieved using separate API calls. We could use async/ await to fetch both these pieces of information:

```cs
public Task<PostDetails> GetPostDetails(int postId)
{
    var postInfo = await FetchPostInfo(postId);
    var postContent = await FetchPostContent(postId);
    return new PostDetails(postInfo, postContent);
}
```

Here, we're making two successive `await` calls, which means the execution will be suspended at the first request- `FetchPostInfo`- and only begin executing the second request- `FetchPostContent`- once the first request has completed.

But fetching `FetchPostContent` doesn't require the result of `FetchPostInfo`, which means we could have started both these requests concurrently! Async/ await lets us write *asynchronous* code in a nice, sequential-looking way, but doesn't let us write *concurrent* code like this. 

### Composing async methods
To make matters worse, we can easily call our inefficient `GetPostDetails` method from another method, compounding the oversequentialization:

```cs
public Task<IEnumerable<PostDetails> LatestPostContent()
{
  var latest = await GetTwoLatestPostIds();
  var first = await GetPostDetails(latest.Item1);
  var second = await GetPostDetails(latest.Item2);
  return new List<PostContent>{first, second};
}
```

Here's what will happen if we execute this method:

- Wait for `GetTwoLatestPostIds`
- Then wait for the first `GetPostDetails` call, which involves:
  - Waiting for `FetchPostInfo`
  - Then waiting for `FetchPostContent`
- Then wait for the second `GetPostDetails` call, again involving:
  - Waiting for `FetchPostInfo`
  - Then waiting for `FetchPostContent`

This code will sequentially execute four calls that could have been executed concurrently!

### What's wrong with `Task.WhenAll`/ `Promise.all`?
We can manually add concurrency by giving up the sequential-looking code, and sprinkling our code with `Task.WhenAll`.

But hang on, async/await was designed to solve these problems:

- Writing asynchronous code is error-prone
- Asynchronous code obscures the meaning of what we're trying to achieve
- Programmers are bad at reasoning about asynchronous code

Giving up our sequential abstraction means these exact problems have reemerged in the context of concurrency!

- Writing **concurrent** code is error-prone
- **Concurrent** code obscures the meaning of what we're trying to achieve
- Programmers are bad at reasoning about **concurrent** code

## Haxl: reclaiming the sequential abstraction
Haxl allows us to write code that *looks* sequential, but is capable of being analyzed to determine the requests we can fetch concurrently, and then automatically batch these requests together.

It also only fetches duplicate requests once, even if the duplicate requests are started concurrently- something we can't achieve with `Task.WhenAll`.

Let's rewrite `GetPostDetails` using HaxlSharp:

```cs
Fetch<PostDetails> GetPostDetails(int postId) =>
    from info in FetchPostInfo(postId)
    from content in FetchPostContent(postId)
    select new PostDetails(info, content);
```

The framework can automatically work out that these calls can be parallelized. Here's the debug log from when we fetch `GetPostDetails(1)`:

```
==== Batch ====
Fetched 'info': PostInfo { Id: 1, Date: 10/06/2016, Topic: 'Topic 1'}
Fetched 'content': Post 1

==== Result ====
PostDetails { Info: PostInfo { Id: 1, Date: 10/06/2016, Topic: 'Topic 1'}, Content: 'Post 1' }

```

Both requests were automatically placed in a single batch and fetched concurrently!

### Composing requests
Let's compose our new `GetPostDetails` function:

```cs
Fetch<List<PostDetails>> GetLatestPostDetails() =>
  from latest in FetchTwoLatestPostIds()
  // We must wait here
  from first in GetPostDetails(latest.Item1)
  from second in GetPostDetails(latest.Item2)
  select new List<PostDetails> { first, second };
```

If we fetch this, we get:

```
==== Batch ====
Fetched 'latest': (0, 1)

==== Batch ====
Fetched 'content': Post 1
Fetched 'info': PostInfo { Id: 1, Date: 10/06/2016, Topic: 'Topic 1'}
Fetched 'content': Post 0
Fetched 'info': PostInfo { Id: 0, Date: 11/06/2016, Topic: 'Topic 0'}

==== Result ====
[ PostDetails { Info: PostInfo { Id: 0, Date: 11/06/2016, Topic: 'Topic 0'}, Content: 'Post 0' },
PostDetails { Info: PostInfo { Id: 1, Date: 10/06/2016, Topic: 'Topic 1'}, Content: 'Post 1' } ]
```

The framework has worked out that we have to wait for the first call's result before continuing, because we rely on this result to execute our subsequent calls. But the subsequent two calls only depend on `latest`, so once `latest` is fetched, they can both be fetched concurrently! 

Note that we made two parallelizable calls to `GetPostDetails`, which is itself made up of two parallelizable requests. These requests were "pulled out" and placed into a single batch of four concurrent requests. Let's see what happens if we rewrite `GetPostDetails` so that it must make two sequential requests:

```cs
Fetch<PostDetails> GetPostDetails(int postId) =>
    from info in FetchPostInfo(postId)
    // We need to wait for the result of info before we can get this id!
    from content in FetchPostContent(info.Id)
    select new PostDetails(info, content);
```

now when we fetch `GetLatestPostDetails`, we get:

```
==== Batch ====
Fetched 'latest': (0, 1)

==== Batch ====
Fetched 'info': PostInfo { Id: 1, Date: 10/06/2016, Topic: 'Topic 1'}
Fetched 'info': PostInfo { Id: 0, Date: 11/06/2016, Topic: 'Topic 0'}

==== Batch ====
Fetched 'content': Post 1
Fetched 'content': Post 0

==== Result ====
[ PostDetails { Info: PostInfo { Id: 0, Date: 11/06/2016, Topic: 'Topic 0'}, Content: 'Post 0' },
PostDetails { Info: PostInfo { Id: 1, Date: 10/06/2016, Topic: 'Topic 1'}, Content: 'Post 1' } ]
```

The `info` requests within `GetPostDetails` can be fetched with just the result of `latest`, so they were batched together. The remaining `content` batch can resume once the `info` batch completes.

### Request deduplication

Because we lazily compose our requests, we can keep track of every subrequest within a particular request, and only fetch a particular subrequest once, even if they're part of the same batch.

Let's say that each post has an author, and each author has three best friends. We could fetch the friends of the author of a given post like this:

```cs
Fetch<IEnumerable<Person>> PostAuthorFriends(int postId) =>
    from info in FetchPostInfo(postId)
    from author in GetPerson(info.AuthorId)
    from friends in author.BestFriendIds.SelectFetch(GetPerson)
    select friends;
```

Here, we're using `SelectFetch`, which lets us run a request for every item in a list, and get back the list of results.  (`SelectFetch` has the signature `[a] -> (a -> Fetch a) -> Fetch [a]`- basically a monomorphic `sequenceA` to Haskellers).

Let's fetch `PostAuthorFriends(3)`:

```
==== Batch ====
Fetched 'info': PostInfo { Id: 3, Date: 8/06/2016, Topic: 'Topic 0'}

==== Batch ====
Fetched 'author': Person { PersonId: 19, Name: Johnie Wengerd, BestFriendIds: [ 10, 12, 14 ]  }

==== Batch ====
Fetched 'friends[2]': Person { PersonId: 14, Name: Michal Zakrzewski, BestFriendIds: [ 5, 7, 9 ]  }
Fetched 'friends[0]': Person { PersonId: 10, Name: Shandra Hanlin, BestFriendIds: [ 13, 15, 17 ]  }
Fetched 'friends[1]': Person { PersonId: 12, Name: Peppa Pig, BestFriendIds: [ 19, 1, 3 ]  }

==== Result ====
[ Person { PersonId: 10, Name: Shandra Hanlin, BestFriendIds: [ 13, 15, 17 ]  },
  Person { PersonId: 12, Name: Peppa Pig, BestFriendIds: [ 19, 1, 3 ]  },
  Person { PersonId: 14, Name: Michal Zakrzewski, BestFriendIds: [ 5, 7, 9 ]  } ]
```

Calling `.SelectFetch(GetPerson)` on a list of three `PersonId`s gets us a list of three `Person` objects. Each item in the list was fetched in a single concurrent batch.

Now let's see how we handle duplicate requests: 

```cs
from ids in FetchTwoLatestPosts()
from friends1 in PostAuthorFriends(ids.Item1)
from friends2 in PostAuthorFriends(ids.Item2)
select friends1.Concat(friends2);
```

Fetching this gives us:

```
==== Batch ====
Fetched 'ids': (3, 4)

==== Batch ====
Fetched 'info': PostInfo { Id: 3, Date: 8/06/2016, Topic: 'Topic 0'}
Fetched 'info': PostInfo { Id: 4, Date: 7/06/2016, Topic: 'Topic 1'}

==== Batch ====
Fetched 'author': Person { PersonId: 12, Name: Peppa Pig, BestFriendIds: [ 19, 1, 3 ]  }
Fetched 'author': Person { PersonId: 19, Name: Johnie Wengerd, BestFriendIds: [ 10, 12, 14 ]  }

==== Batch ====
Fetched 'friends[0]': Person { PersonId: 10, Name: Shandra Hanlin, BestFriendIds: [ 13, 15, 17 ]  }
Fetched 'friends[2]': Person { PersonId: 3, Name: Corazon Benito, BestFriendIds: [ 2, 4, 6 ]  }
Fetched 'friends[1]': Person { PersonId: 1, Name: Cristal Hornak, BestFriendIds: [ 16, 18, 0 ]  }
Fetched 'friends[2]': Person { PersonId: 14, Name: Michal Zakrzewski, BestFriendIds: [ 5, 7, 9 ]  }

==== Result ====
[ Person { PersonId: 10, Name: Shandra Hanlin, BestFriendIds: [ 13, 15, 17 ]  },
  Person { PersonId: 12, Name: Peppa Pig, BestFriendIds: [ 19, 1, 3 ]  },
  Person { PersonId: 14, Name: Michal Zakrzewski, BestFriendIds: [ 5, 7, 9 ]  },
  Person { PersonId: 1, Name: Cristal Hornak, BestFriendIds: [ 16, 18, 0 ]  },
  Person { PersonId: 19, Name: Johnie Wengerd, BestFriendIds: [ 10, 12, 14 ]  },
  Person { PersonId: 3, Name: Corazon Benito, BestFriendIds: [ 2, 4, 6 ]  } ]
```

Because Peppa Pig and Johnie Wengerd are each other's best friends, we don't need to fetch them again when we're fetching their best friends. The fourth batch, where the best friends of Peppa and Johnie are fetched, only contains four requests, but the results are still correctly compiled into a list of six best friends.

This is also helpful for consistency; even though data can change during a fetch, we can still ensure that we don't get multiple versions of the same data within a single fetch.

## Implementation details
The [original paper](http://community.haskell.org/~simonmar/papers/haxl-icfp14.pdf) gives a good overview of Haxl. Some differences between the C# and Haskell version are documented [here](http://joashc.github.io/posts/2016-06-11-haxlsharp.html).

## Integration
The default API is similar to ServiceStack's, but it's straightforward to implement your own API if this one is not to your taste. You can implement your own API to HaxlSharp by just installing the `HaxlSharp.Core` package, instead of the `HaxlSharp` package, and implementing your own fetcher/ caching strategy. See [Implementing your own fetcher](#implementing-your-own-fetcher) for more details.

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
This library operates on `Fetch<>` objects, so we write functions that create `Returns<>` objects and then call `ToFetch` on them:

```cs
Fetch<PostInfo> GetPostInfo(int postId) => new GetPostInfo(postId).ToFetch();
Fetch<PostContent> GetPostContent(int postId) => new GetPostContent(postId).ToFetch();
```

Now we can compose any function that returns a `Fetch<>`, and they'll be automatically batched as much as possible:

```cs
Fetch<PostDetails> GetPostDetails(int postId) =>
    from info in GetPostInfo(postId)
    from content in GetPostContent(postId)
    select new PostDetails(info, content);

Fetch<IEnumerable<PostDetails>> RecentPostDetails() =>
    from postIds in GetAllPostIds()
    from postDetails in postIds.Take(10).SelectFetch(GetPostDetails)
    select postDetails;
```

### Handling requests
Of course, the data must come from somewhere, so we must create handlers for every request type. Handlers are just functions from the request type to the response type. Register these functions to create a `Fetcher` object:

```cs
var fetcher = FetcherBuilder.New()
  .FetchRequest<FetchPosts, IEnumerable<int>>(_ => _postApi.GetAllPostIds())
  .FetchRequest<FetchPostInfo, PostInfo>(req => _postApi.GetPostInfo(req.PostId))
  .FetchRequest<FetchUser, User>(req => _userApi.GetUser(req.UserId))
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

We should work within the `Fetch<>` monad as much as possible, and only resolve the final `Fetch<>` when absolutely necessary. This ensures the framework performs the fetches in the most efficient way.

### Implementing your own fetcher
This library comes with a default fetching and caching strategy, but it's possible to implement your own.

#### Why would you want to implement your own fetcher/ caching strategy?

- You think there's too much boilerplate with the default fetcher. The current implementation is optimized to make it easy to integrate with existing request objects, at the cost of slightly more boilerplate.
- You don't want the overhead of serializing every request object to get a cache key, and/ or already have a way to create cache keys from your request objects.
- You can do something clever with a batch of requests- you might bundle them up and send them to a particular server, for example.
- You want to inject default values of failed requests

#### Fetcher
The fetcher interface mainly requires you to implement: 

```cs
Task FetchBatch(IEnumerable<BlockedRequest> requests);
```

A blocked request contains a request object and a `TaskCompletionSource`, which allows us to manually create and resolve `Task` objects- think Javascript promises. You'll want to map the list of blocked requests to a list of `Task`s, and manually resolve each one with the result of its respective request. 

Then you just return `Task.WhenAll` on the list of tasks!

#### Caching
To customize the caching behaviour, you need  to implement a function that returns a cache key for a request:

```cs
string ForRequest<A>(Returns<A> request);
```

Note that this is not traditional caching- it's intrarequest caching used for request deduplication and consistency. You'll still want to have a traditional caching layer.

## Limitations
This library is still in its very early stages! Here is a very incomplete list of its limitations:

### Speed
This is the most important one: it's still very unoptimized, so it adds an overhead that might not pay for itself!

Queries written in the `Fetch` monad are actually treated as expression trees so they can be analysed to determine their dependencies. The expression trees are rewritten to maximise concurrency, and then compiled. Unfortunately, expression tree compilation is *slow* in C\#!

This makes `SelectFetch` very inefficient on larger lists, because it compiles multiple expression trees for each item in the list. The Haskell version seems to have a similar asymptotic complexity, but with a *much* smaller constant.

(Current plan for optimizing: instead of compiling an expression tree for each item in the list `[a]`, I could compile the expression `a -> Expression` once, and then plug `a` into this compiled expression. We'll see how this works out.)

### Anonymous types
It's not recommended to return anonymous types from your `Fetch` functions, unless you want your functions to fail unpredictably. C\# uses anonymous types internally for query expressions, which is alright in the case of [transparent identifiers](http://joashc.github.io/posts/2016-03-17-select-many-weird.html) because they're tagged with a special name only available to the compiler, but `let` statements are translated into plain old anonymous types that are indistinguishable from the ones you could type in manually:

```cs
from a in x
from b in y
select new {a, b};
```

There a few checks in place so anonymous types won't fail in all circumstances, but unless you want to memorize this list and ensure your expression doesn't meet all of these criteria:

- Expression body is `ExpressionType.New`
- Creates an anonymous type
- Anonymous type has exactly two properties
- First property of anonymous type is the same as the first parameter of the expression 

...you're better off just not using anonymous types.

### Applicative Do
We're currently using a simplified version of the `ApplicativeDo` algorithm in GHC, so a query expression like:

```cs
from x in a
from y in b(x)
from z in c
```

is executed in three batches, even though `a` and `c` could be started concurrently.

### It's a giant hack
The C# language spec goes to the effort of saying that the implementation details of query expression rewriting and scoping are *not* part of the specification, and different implementations of C# can do query rewriting/scoping differently.

Of course, this library builds heavily on the internal, unspecified implementation details of query rewriting and scoping, so it's possible that the C\# team could reimplement it and break the library.

I think the C# team kept transparent identifiers, etc. out of the spec because they knew they were a bit of a hack to get the desired transitive scoping behaviour, which actually *is* part of the spec. So this library is a hack raised upon a hack... but it's called HaxlSharp, so at least the name is apt.
