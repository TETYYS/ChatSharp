using ChatSharp.Events;
using System.Linq;
using System.Threading.Tasks;

namespace ChatSharp.Handlers
{
    /// <summary>
    /// IRC error replies handler. See rfc1459 6.1.
    /// </summary>
    internal static class ErrorHandlers
    {
        /// <summary>
        /// IRC Error replies handler. See rfc1459 6.1.
        /// </summary>
        public static ValueTask HandleError(IrcClient client, IrcMessage message)
        {
            client.OnErrorReply(new Events.ErrorReplyEventArgs(message));

            return default;
        }
    }
}
