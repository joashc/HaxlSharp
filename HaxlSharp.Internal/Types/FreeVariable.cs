
namespace HaxlSharp.Internal
{
    /// <summary>
    /// A free variable that might come from a transparent identifier.
    /// </summary>
    public class FreeVariable
    {
        public readonly bool FromTransparent;
        public readonly string Name;
        public FreeVariable(string name, bool fromTransparent)
        {
            Name = name;
            FromTransparent = fromTransparent;
        }
    }
}
