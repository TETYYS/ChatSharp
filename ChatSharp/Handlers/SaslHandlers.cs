using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    internal static class SaslHandlers
    {
        public static async ValueTask HandleAuthentication(IrcClient client, IrcMessage message)
        {
            if (client.IsAuthenticatingSasl)
            {
                if (message.Parameters[0] == "+")
                {
                    // Based off irc-framework implementation
                    var plainString = string.Format("{0}\0{0}\0{1}", client.User.Nick, client.User.Password);
                    var b64Bytes = Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(plainString)));

                    while (b64Bytes.Length >= 400)
                    {
                        var chunk = b64Bytes.Take(400).ToArray();
                        b64Bytes = b64Bytes.Skip(400).ToArray();
                        await client.SendRawMessage(string.Format("AUTHENTICATE {0}", Encoding.UTF8.GetString(chunk)));
                    }
                    if (b64Bytes.Length > 0)
                        await client.SendRawMessage(string.Format("AUTHENTICATE {0}", Encoding.UTF8.GetString(b64Bytes)));
                    else
                        await client.SendRawMessage("AUTHENTICATE +");

                    client.IsAuthenticatingSasl = false;
                }
            }
        }

        public static async ValueTask HandleError(IrcClient client, IrcMessage message)
        {
            if (client.IsNegotiatingCapabilities && !client.IsAuthenticatingSasl)
            {
                await client.SendRawMessage("CAP END");
                client.IsNegotiatingCapabilities = false;
            }
        }
    }
}
