namespace HaxlSharp
{
    /// <summary>
    /// A request object annotated with a return type.
    /// </summary>
    /// <typeparam name="A">The return type of the request.</typeparam>
    public interface Returns<A> { }

    public static class RequestExt
    {
        public static Fetch<A> ToFetch<A>(this Returns<A> request)
        {
            return new Request<A>(request);
        }
    }
}
