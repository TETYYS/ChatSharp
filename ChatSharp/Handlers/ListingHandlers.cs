using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    internal static class ListingHandlers
    {
        public static void HandleBanListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' '));
            var parameters = parameterString.Substring(parameterString.IndexOf(' ')).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE b " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleBanListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE b " + message.Parameters[1]);
        }

        public static void HandleExceptionListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE e " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleExceptionListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE e " + message.Parameters[1]);
        }

        public static void HandleInviteListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE I " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleInviteListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE I " + message.Parameters[1]);
        }

        public static void HandleQuietListPart(IrcClient client, IrcMessage message)
        {
            var parameterString = message.RawMessage.Substring(message.RawMessage.IndexOf(' ') + 1);
            var parameters = parameterString.Substring(parameterString.IndexOf(' ') + 1).Split(' ');
            var list = (MaskCollection)client.RequestManager.GetState("GETMODE q " + parameters[1]);
            list.Add(new Mask(parameters[2], client.Users.GetOrAdd(parameters[3]),
                IrcClient.DateTimeFromIrcTime(int.Parse(parameters[4]))));
        }

        public static void HandleQuietListEnd(IrcClient client, IrcMessage message)
        {
            client.RequestManager.CompleteOperation("GETMODE q " + message.Parameters[1]);
        }
    }
}
