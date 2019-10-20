using ChatSharp.Events;
using System;
using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    internal static class MOTDHandlers
    {
        public static string MOTD { get; set; }

        public static ValueTask HandleMOTDStart(IrcClient client, IrcMessage message)
        {
            MOTD = string.Empty;

            return default;
        }

        public static ValueTask HandleMOTD(IrcClient client, IrcMessage message)
        {
            if (message.Parameters.Length != 2)
                throw new IrcProtocolException("372 MOTD message is incorrectly formatted.");
            var part = message.Parameters[1].Substring(2);
            MOTD += part + Environment.NewLine;
            client.OnMOTDPartRecieved(new ServerMOTDEventArgs(part));

            return default;
        }

        public static async ValueTask HandleEndOfMOTD(IrcClient client, IrcMessage message)
        {
            client.OnMOTDRecieved(new ServerMOTDEventArgs(MOTD));
            client.OnConnectionComplete(new EventArgs());
            // Verify our identity
            await VerifyOurIdentity(client);
        }

	    public static async ValueTask HandleMOTDNotFound(IrcClient client, IrcMessage message)
	    {
            client.OnMOTDRecieved(new ServerMOTDEventArgs(MOTD));
            client.OnConnectionComplete(new EventArgs());

            await VerifyOurIdentity(client);
        }

	    private static async ValueTask VerifyOurIdentity(IrcClient client)
	    {
            if (client.Settings.WhoIsOnConnect)
            {
                var whois = await client.WhoIs(client.User.Nick);
                client.User.Nick = whois.User.Nick;
                client.User.User = whois.User.User;
                client.User.Hostname = whois.User.Hostname;
                client.User.RealName = whois.User.RealName;
            }
        }
    }
}
