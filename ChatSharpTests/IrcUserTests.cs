using Xunit;

namespace ChatSharp.Tests
{
    public class IrcUserTests
    {
        [Fact]
        public void GetUserModes_NotNull_FiveModes()
        {
            var user = new IrcUser("~&@%+aji", "user");
            var client = new IrcClient("irc.address", user);

            var userModes = client.ServerInfo.GetModesForNick(user.Nick);

            Assert.True(userModes.Count == 5);
        }

        [Fact]
        public void GetUserModes_NotNull_FourModes()
        {
            var user = new IrcUser("&@%+aji", "user");
            var client = new IrcClient("irc.address", user);

            var userModes = client.ServerInfo.GetModesForNick(user.Nick);

            Assert.True(userModes.Count == 4);
        }

        [Fact]
        public void GetUserModes_NotNull_ThreeModes()
        {
            var user = new IrcUser("@%+aji", "user");
            var client = new IrcClient("irc.address", user);

            var userModes = client.ServerInfo.GetModesForNick(user.Nick);

            Assert.True(userModes.Count == 3);
        }

        [Fact]
        public void GetUserModes_NotNull_TwoModes()
        {
            var user = new IrcUser("%+aji", "user");
            var client = new IrcClient("irc.address", user);

            var userModes = client.ServerInfo.GetModesForNick(user.Nick);

            Assert.True(userModes.Count == 2);
        }

        [Fact]
        public void GetUserModes_NotNull_OneMode()
        {
            var user = new IrcUser("+aji", "user");
            var client = new IrcClient("irc.address", user);

            var userModes = client.ServerInfo.GetModesForNick(user.Nick);

            Assert.True(userModes.Count == 1);
        }

        [Fact]
        public void GetUserModes_IsNull()
        {
            var user = new IrcUser("aji", "user");
            var client = new IrcClient("irc.address", user);

            var userModes = client.ServerInfo.GetModesForNick(user.Nick);

            Assert.True(userModes.Count == 0);
        }
    }
}
