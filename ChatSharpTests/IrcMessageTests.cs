using System;
using ChatSharp;
using System.Collections.Generic;
using Xunit;

namespace ChatSharp.Tests
{
    public class IrcMessageTests
    {
        [Fact]
        public void NewValidMessage()
        {
            new IrcMessage(@":user!~ident@host PRIVMSG target :Lorem ipsum dolor sit amet");
        }

        [Fact]
        public void NewValidMessage_Command()
        {
            IrcMessage fromMessage = new IrcMessage(@":user!~ident@host PRIVMSG target :Lorem ipsum dolor sit amet");
            Assert.Equal("PRIVMSG", fromMessage.Command);
        }

        [Fact]
        public void NewValidMessage_Prefix()
        {
            IrcMessage fromMessage = new IrcMessage(@":user!~ident@host PRIVMSG target :Lorem ipsum dolor sit amet");
            Assert.Equal("user!~ident@host", fromMessage.Prefix);
        }

        [Fact]
        public void NewValidMessage_Params()
        {
            IrcMessage fromMessage = new IrcMessage(@":user!~ident@host PRIVMSG target :Lorem ipsum dolor sit amet");
            string[] compareParams = new string[] { "target", "Lorem ipsum dolor sit amet" };
            Assert.Equal(compareParams, fromMessage.Parameters);
        }

        [Fact]
        public void NewValidMessage_Tags()
        {
            IrcMessage fromMessage = new IrcMessage("@a=123;b=456;c=789 :user!~ident@host PRIVMSG target :Lorem ipsum dolor sit amet");
            KeyValuePair<string, string>[] compareTags = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("a", "123"),
                new KeyValuePair<string, string>("b", "456"),
                new KeyValuePair<string, string>("c", "789")
            };
            Assert.Equal(compareTags, fromMessage.Tags);
        }

        [Fact]
        public void NewValidMessage_Tags02()
        {
            IrcMessage fromMessage = new IrcMessage("@aaa=bbb;ccc;example.com/ddd=eee :nick!ident@host.com PRIVMSG me :Hello");
            KeyValuePair<string, string>[] compareTags = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("aaa", "bbb"),
                new KeyValuePair<string, string>("ccc", null),
                new KeyValuePair<string, string>("example.com/ddd", "eee"),
            };
            Assert.Equal(compareTags, fromMessage.Tags);
        }

        [Fact]
        public void NewValidMessage_TagsWithSemicolon()
        {
            IrcMessage fromMessage = new IrcMessage(@"@a=123\:456;b=456\:789;c=789\:123 :user!~ident@host PRIVMSG target :Lorem ipsum dolor sit amet");
            KeyValuePair<string, string>[] compareTags = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("a", "123;456"),
                new KeyValuePair<string, string>("b", "456;789"),
                new KeyValuePair<string, string>("c", "789;123"),
            };
            Assert.Equal(compareTags, fromMessage.Tags);
        }

        [Fact]
        public void NewValidMessage_TagsNoValue()
        {
            IrcMessage fromMessage = new IrcMessage("@a=;b :nick!ident@host.com PRIVMSG me :Hello");
            KeyValuePair<string, string>[] compareTags = new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string, string>("a", ""),
                new KeyValuePair<string, string>("b", null),
            };
            Assert.Equal(compareTags, fromMessage.Tags);
        }

        [Fact]
        public void Timestamp_CompareISOString()
        {
            IrcMessage[] messages = {
                new IrcMessage("@time=2011-10-19T16:40:51.620Z :Angel!angel@example.org PRIVMSG Wiz :Hello"),
                new IrcMessage("@time=2012-06-30T23:59:59.419Z :John!~john@1.2.3.4 JOIN #chan")
            };

            string[] timestamps = {
                "2011-10-19T16:40:51.620Z",
                "2012-06-30T23:59:59.419Z"
            };

            Assert.Equal(timestamps[0], messages[0].Timestamp.ToISOString());
            Assert.Equal(timestamps[1], messages[1].Timestamp.ToISOString());
        }

        [Fact]
        public void Timestamp_FromTimestamp()
        {
            IrcMessage[] messages = {
                new IrcMessage("@t=1504923966 :Angel!angel@example.org PRIVMSG Wiz :Hello"),
                new IrcMessage("@t=1504923972 :John!~john@1.2.3.4 JOIN #chan")
            };

            string[] timestamps = {
                "2017-09-09T02:26:06.000Z",
                "2017-09-09T02:26:12.000Z"
            };

            Assert.Equal(timestamps[0], messages[0].Timestamp.ToISOString());
            Assert.Equal(timestamps[1], messages[1].Timestamp.ToISOString());
        }

        [Fact]
        public void Timestamp_FailOnLeap()
        {
            Assert.Throws<ArgumentException>(() => new IrcMessage("@time=2012-06-30T23:59:60.419Z :John!~john@1.2.3.4 JOIN #chan"));
        }
    }
}
