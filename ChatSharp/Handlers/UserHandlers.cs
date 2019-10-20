using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using static ChatSharp.RequestManager;

namespace ChatSharp.Handlers
{
    internal static class UserHandlers
    {
        public static ValueTask HandleWhoIsUser(IrcClient client, IrcMessage message)
        {
            if (message.Parameters != null && message.Parameters.Length >= 6)
            {
                var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);
                whois.User.Nick = message.Parameters[1];
                whois.User.User = message.Parameters[2];
                whois.User.Hostname = message.Parameters[3];
                whois.User.RealName = message.Parameters[5];
                if (client.Users.Contains(whois.User.Nick))
                {
                    var user = client.Users[whois.User.Nick];
                    user.User = whois.User.User;
                    user.Hostname = whois.User.Hostname;
                    user.RealName = whois.User.RealName;
                    whois.User = user;
                }
            }

            return default;
        }

        public static ValueTask HandleWhoIsLoggedInAs(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);
            whois.LoggedInAs = message.Parameters[2];

            return default;
        }

        public static ValueTask HandleWhoIsServer(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);
            whois.Server = message.Parameters[2];
            whois.ServerInfo = message.Parameters[3];

            return default;
        }

        public static ValueTask HandleWhoIsOperator(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);
            whois.IrcOp = true;

            return default;
        }

        public static ValueTask HandleWhoIsIdle(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);
            whois.SecondsIdle = int.Parse(message.Parameters[2]);

            return default;
        }

        public static ValueTask HandleWhoIsChannels(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);
            var channels = message.Parameters[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < channels.Length; i++)
                if (!channels[i].StartsWith("#"))
                    channels[i] = channels[i].Substring(1);
            whois.Channels = whois.Channels.Concat(channels).ToArray();

            return default;
        }

        public static ValueTask HandleWhoIsEnd(IrcClient client, IrcMessage message)
        {
            var whois = (WhoIs)client.RequestManager.GetState("WHOIS " + message.Parameters[1]);

            if (!client.Users.Contains(whois.User.Nick))
                client.Users.Add(whois.User);

            client.RequestManager.CompleteOperation("WHOIS " + message.Parameters[1]);
            client.OnWhoIsReceived(new Events.WhoIsReceivedEventArgs(whois));

            return default;
        }

        public static ValueTask HandleWho(IrcClient client, IrcMessage message)
        {
            // A standard WHO request (numeric 352) is just like a WHOX request, except that it has less fields.
            foreach (var query in client.RequestManager.PendingOperations.Where(kvp => kvp.Key.StartsWith("WHO ")))
            {
                if (query.Key != string.Empty && query.Value != null)
                {
                    var whoList = (List<ExtendedWho>)client.RequestManager.GetState(query.Key);
                    var who = new ExtendedWho();

                    who.Channel = message.Parameters[1];
                    who.User.User = message.Parameters[2];
                    who.IP = message.Parameters[3];
                    who.Server = message.Parameters[4];
                    who.User.Nick = message.Parameters[5];
                    who.Flags = message.Parameters[6];


                    var supposedRealName = message.Parameters[7];

                    // Parsing IRC spec craziness: the hops field is included in the real name field
                    var hops = supposedRealName.Substring(0, supposedRealName.IndexOf(" "));
                    who.Hops = int.Parse(hops);

                    var realName = supposedRealName.Substring(supposedRealName.IndexOf(" ") + 1);
                    who.User.RealName = realName;

                    whoList.Add(who);
                }
            }

            return default;
        }

        public static ValueTask HandleWhox(IrcClient client, IrcMessage message)
        {
            int msgQueryType = int.Parse(message.Parameters[1]);
            var whoxQuery = new KeyValuePair<string, RequestOperation>();

            foreach (var query in client.RequestManager.PendingOperations.Where(kvp => kvp.Key.StartsWith("WHO ")))
            {
                // Parse query to retrieve querytype
                string key = query.Key;
                string[] queryParts = key.Split(new[] { ' ' });

                int queryType = int.Parse(queryParts[2]);

                // Check querytype against message querytype
                if (queryType == msgQueryType) whoxQuery = query;
            }

            if (whoxQuery.Key != string.Empty && whoxQuery.Value != null)
            {
                var whoxList = (List<ExtendedWho>)client.RequestManager.GetState(whoxQuery.Key);
                var whox = new ExtendedWho();

                string key = whoxQuery.Key;
                string[] queryParts = key.Split(new[] { ' ' });

                // Handle what fields were included in the WHOX request
                WhoxField fields = (WhoxField)int.Parse(queryParts[3]);
                int fieldIdx = 1;
                do
                {
                    if ((fields & WhoxField.QueryType) != 0)
                    {
                        whox.QueryType = msgQueryType;
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Channel) != 0)
                    {
                        whox.Channel = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Username) != 0)
                    {
                        whox.User.User = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.UserIp) != 0)
                    {
                        whox.IP = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Hostname) != 0)
                    {
                        whox.User.Hostname = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.ServerName) != 0)
                    {
                        whox.Server = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Nick) != 0)
                    {
                        whox.User.Nick = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Flags) != 0)
                    {
                        whox.Flags = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.Hops) != 0)
                    {
                        whox.Hops = int.Parse(message.Parameters[fieldIdx]);
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.TimeIdle) != 0)
                    {
                        whox.TimeIdle = int.Parse(message.Parameters[fieldIdx]);
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.AccountName) != 0)
                    {
                        whox.User.Account = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.OpLevel) != 0)
                    {
                        whox.OpLevel = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }

                    if ((fields & WhoxField.RealName) != 0)
                    {
                        whox.User.RealName = message.Parameters[fieldIdx];
                        fieldIdx++;
                    }
                }
                while (fieldIdx < message.Parameters.Length - 1);
                whoxList.Add(whox);
            }

            return default;
        }

        public static ValueTask HandleLoggedIn(IrcClient client, IrcMessage message)
        {
            client.User.Account = message.Parameters[2];

            return default;
        }

        public static ValueTask HandleWhoEnd(IrcClient client, IrcMessage message)
        {
            if (client.ServerInfo.ExtendedWho)
            {
                var query = client.RequestManager.PendingOperations.Where(kvp => kvp.Key.StartsWith("WHO " + message.Parameters[1])).FirstOrDefault();

                var whoxList = (List<ExtendedWho>)client.RequestManager.GetState(query.Key);

                foreach (var whox in whoxList)
                    if (!client.Users.Contains(whox.User.Nick))
                        client.Users.Add(whox.User);

                client.RequestManager.CompleteOperation(query.Key);
                client.OnWhoxReceived(new Events.WhoxReceivedEventArgs(whoxList.ToArray()));
            }
            else
            {
                var query = client.RequestManager.PendingOperations.Where(kvp => kvp.Key == "WHO " + message.Parameters[1]).FirstOrDefault();

                var whoList = (List<ExtendedWho>)client.RequestManager.GetState(query.Key);

                foreach (var who in whoList)
                    if (!client.Users.Contains(who.User.Nick))
                        client.Users.Add(who.User);

                client.RequestManager.CompleteOperation(query.Key);
                client.OnWhoxReceived(new Events.WhoxReceivedEventArgs(whoList.ToArray()));
            }

            return default;
        }

        public static ValueTask HandleAccount(IrcClient client, IrcMessage message)
        {
            var user = client.Users.GetOrAdd(message.Prefix);
            user.Account = message.Parameters[0];

            return default;
        }

        public static ValueTask HandleChangeHost(IrcClient client, IrcMessage message)
        {
            var user = client.Users.Get(message.Prefix);

            // Only handle CHGHOST for users we know
            if (user != null)
            {
                var newIdent = message.Parameters[0];
                var newHostname = message.Parameters[1];

                if (user.User != newIdent)
                    user.User = newIdent;
                if (user.Hostname != newHostname)
                    user.Hostname = newHostname;
            }

            return default;
        }
    }
}
