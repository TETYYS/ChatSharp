using ChatSharp.Events;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    internal static class ChannelHandlers
    {
        public static async ValueTask HandleJoin(IrcClient client, IrcMessage message)
        {
            var user = client.Users.GetOrAdd(message.Prefix);
            var channel = client.Channels.GetOrAdd(message.Parameters[0]);

            if (channel != null)
            {
                if (!user.Channels.Contains(channel))
                    user.Channels.Add(channel);

                // account-notify capability
                if (client.Capabilities.IsEnabled("account-notify"))
                {
                    var whoQuery = await client.Who(user.Nick, WhoxFlag.None, WhoxField.Nick | WhoxField.AccountName);
                    if (whoQuery.Count == 1)
                        user.Account = whoQuery[0].User.Account;
                }

                client.OnUserJoinedChannel(new ChannelUserEventArgs(channel, user));
            }
        }

        public static ValueTask HandleGetTopic(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels.GetOrAdd(message.Parameters[1]);
            var old = channel._Topic;
            channel._Topic = message.Parameters[2];
            client.OnChannelTopicReceived(new ChannelTopicEventArgs(channel, old, channel._Topic));

            return default;
        }

        public static ValueTask HandleGetEmptyTopic(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels.GetOrAdd(message.Parameters[1]);
            var old = channel._Topic;
            channel._Topic = message.Parameters[2];
            client.OnChannelTopicReceived(new ChannelTopicEventArgs(channel, old, channel._Topic));

            return default;
        }

        public static ValueTask HandlePart(IrcClient client, IrcMessage message)
        {
            if (!client.Channels.Contains(message.Parameters[0]))
                return default; // we aren't in this channel, ignore

            var user = client.Users.Get(message.Prefix);
            var channel = client.Channels[message.Parameters[0]];

            if (user.Channels.Contains(channel))
                user.Channels.Remove(channel);
            if (user.ChannelModes.ContainsKey(channel))
                user.ChannelModes.Remove(channel);

            client.OnUserPartedChannel(new ChannelUserEventArgs(channel, user));

            return default;
        }

        public static ValueTask HandleUserListPart(IrcClient client, IrcMessage message)
        {
            if (client.Capabilities.IsEnabled("userhost-in-names"))
            {
                var channel = client.Channels[message.Parameters[2]];
                var users = message.Parameters[3].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var hostmask in users)
                {
                    if (string.IsNullOrWhiteSpace(hostmask))
                        continue;

                    // Parse hostmask
                    var nick = hostmask.Substring(0, hostmask.IndexOf("!"));
                    var ident = hostmask.Substring(nick.Length + 1, hostmask.LastIndexOf("@") - (nick.Length + 1));
                    var hostname = hostmask.Substring(hostmask.LastIndexOf("@") + 1);

                    // Get user modes
                    var modes = client.ServerInfo.GetModesForNick(nick);
                    if (modes.Count > 0)
                        nick = nick.Remove(0, modes.Count);

                    var user = client.Users.GetOrAdd(nick);
                    if (user.Hostname != hostname && user.User != ident)
                    {
                        user.Hostname = hostname;
                        user.User = ident;
                    }

                    if (!user.Channels.Contains(channel))
                        user.Channels.Add(channel);
                    if (!user.ChannelModes.ContainsKey(channel))
                        user.ChannelModes.Add(channel, modes);
                    else
                        user.ChannelModes[channel] = modes;
                }
            }
            else
            {
                var channel = client.Channels[message.Parameters[2]];
                var users = message.Parameters[3].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawNick in users)
                {
                    if (string.IsNullOrWhiteSpace(rawNick))
                        continue;

                    var nick = rawNick;
                    var modes = client.ServerInfo.GetModesForNick(nick);

                    if (modes.Count > 0)
                        nick = rawNick.Remove(0, modes.Count);

                    var user = client.Users.GetOrAdd(nick);

                    if (!user.Channels.Contains(channel))
                        user.Channels.Add(channel);
                    if (!user.ChannelModes.ContainsKey(channel))
                        user.ChannelModes.Add(channel, modes);
                    else
                        user.ChannelModes[channel] = modes;
                }
            }

            return default;
        }

        public static async ValueTask HandleUserListEnd(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels[message.Parameters[1]];
            client.OnChannelListRecieved(new ChannelEventArgs(channel));
            if (client.Settings.ModeOnJoin)
            {
                try
                {
                    await client.GetMode(channel.Name);
                }
                catch { /* who cares */ }
            }
            if (client.Settings.WhoIsOnJoin)
            {
                _ = WhoIsChannel(channel, client);
            }
        }

        private static async ValueTask WhoIsChannel(IrcChannel channel, IrcClient client)
        {
            // Note: joins and parts that happen during this will cause strange behavior here
            await Task.Delay(client.Settings.JoinWhoIsDelay * 1000); // TODO: get rid of this abomination

            foreach (var user in channel.Users)
            {
                var whois = await client.WhoIs(user.Nick);
                user.User = whois.User.User;
                user.Hostname = whois.User.Hostname;
                user.RealName = whois.User.RealName;
            }
        }

        public static ValueTask HandleKick(IrcClient client, IrcMessage message)
        {
            var channel = client.Channels[message.Parameters[0]];
            var kicked = channel.Users[message.Parameters[1]];
            if (kicked.Channels.Contains(channel))
                kicked.Channels.Remove(channel);
            client.OnUserKicked(new KickEventArgs(channel, new IrcUser(message.Prefix),
                kicked, message.Parameters[2]));

            return default;
        }
    }
}
