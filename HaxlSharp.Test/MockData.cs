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
        public readonly int AuthorId;
        public PostInfo(int postId, DateTime postDate, string postTopic, int authorId)
        {
            PostId = postId;
            PostDate = postDate;
            PostTopic = postTopic;
            AuthorId = authorId;
        }

        public override string ToString()
        {
            return $"PostInfo {{ Id: {PostId}, Date: {PostDate.ToShortDateString()}, Topic: '{PostTopic}'}}";
        }
    }
}