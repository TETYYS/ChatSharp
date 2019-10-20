using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    internal static class ListingHandlers
    {
        public static ValueTask HandleBanListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' '));
            var parameters = parameterString.Substring(parameterString.IndexOf(' ')).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE b " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));

            return default;
        }

        public static ValueTask HandleBanListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE b " + message.Parameters[1]);

            return default;
        }

        public static ValueTask HandleExceptionListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE e " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));

            return default;
        }

        public static ValueTask HandleExceptionListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE e " + message.Parameters[1]);

            return default;
        }

        public static ValueTask HandleInviteListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE I " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));

            return default;
        }

        public static ValueTask HandleInviteListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE I " + message.Parameters[1]);

            return default;
        }

        public static ValueTask HandleQuietListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE q " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));

            return default;
        }

        public static ValueTask HandleQuietListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE q " + message.Parameters[1]);

            return default;
        }
    }
}
