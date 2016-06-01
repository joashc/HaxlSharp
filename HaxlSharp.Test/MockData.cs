using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HaxlSharp.Test
{
    public class PostInfo
    {
        public readonly int PostId;
        public readonly DateTime PostDate;
        public readonly string PostTopic;
        public PostInfo(int postId, DateTime postDate, string postTopic)
        {
            PostId = postId;
            PostDate = postDate;
            PostTopic = postTopic;
        }

        public override string ToString()
        {
            return $"PostInfo {{ PostId: {PostId}, PostDate: {PostDate}, PostTopic: {PostTopic}}}";
        }
    }
}