using ChatSharp.Events;
using ChatSharp.Handlers;
using Nito.AsyncEx;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Timers;

namespace ChatSharp
{
    /// <summary>
    /// An IRC client.
    /// </summary>
    public sealed partial class IrcClient
    {
        /// <summary>
        /// A raw IRC message handler.
        /// </summary>
        public delegate void MessageHandler(IrcClient client, IrcMessage message);
        private Dictionary<string, MessageHandler> Handlers { get; set; }

        /// <summary>
        /// Sets a custom handler for an IRC message. This applies to the low level IRC protocol,
        /// not for private messages.
        /// </summary>
        public void SetHandler(string message, MessageHandler handler)
        {
#if DEBUG
            // This is the default behavior if 3rd parties want to handle certain messages themselves
            // However, if it happens from our own code, we probably did something wrong
            if (Handlers.ContainsKey(message.ToUpper()))
                Console.WriteLine("Warning: {0} handler has been overwritten", message);
#endif
            message = message.ToUpper();
            Handlers[message] = handler;
        }

        internal static DateTime DateTimeFromIrcTime(int time)
        {
            return new DateTime(1970, 1, 1).AddSeconds(time);
        }

        internal static Random RandomNumber { get; private set; }
        private string ServerHostname { get; set; }
        private int ServerPort { get; set; }
        private System.Timers.Timer PingTimer { get; set; }
        private TcpClient TcpClient { get; set; }

        internal RequestManager RequestManager { get; set; }

        internal string ServerNameFromPing { get; set; }

        /// <summary>
        /// The address this client is connected to, or will connect to. Setting this
        /// after the client is connected will not cause a reconnect.
        /// </summary>
        public string ServerAddress
        {
            get
            {
                return ServerHostname + ":" + ServerPort;
            }
            internal set
            {
                string[] parts = value.Split(':');
                if (parts.Length > 2 || parts.Length == 0)
                    throw new FormatException("Server address is not in correct format ('hostname:port')");
                ServerHostname = parts[0];
                if (parts.Length > 1)
                    ServerPort = int.Parse(parts[1]);
                else
                    ServerPort = 6667;
            }
        }

        /// <summary>
        /// The low level TCP stream for this client.
        /// </summary>
        public Stream NetworkStream { get; set; }
        /// <summary>
        /// If true, SSL will be used to connect.
        /// </summary>
        public bool UseSSL { get; private set; }
        /// <summary>
        /// If true, invalid SSL certificates are ignored.
        /// </summary>
        public bool IgnoreInvalidSSL { get; set; }
        /// <summary>
        /// The character encoding to use for the connection. Defaults to UTF-8.
        /// </summary>
        /// <value>The encoding.</value>
        public Encoding Encoding { get; set; }
        /// <summary>
        /// The user this client is logged in as.
        /// </summary>
        /// <value>The user.</value>
        public IrcUser User { get; set; }
        /// <summary>
        /// The channels this user is joined to.
        /// </summary>
        public ChannelCollection Channels { get; private set; }
        /// <summary>
        /// Settings that control the behavior of ChatSharp.
        /// </summary>
        public ClientSettings Settings { get; set; }
        /// <summary>
        /// Information about the server we are connected to. Servers may not send us this information,
        /// but it's required for ChatSharp to function, so by default this is a guess. Handle
        /// IrcClient.ServerInfoRecieved if you'd like to know when it's populated with real information.
        /// </summary>
        public ServerInfo ServerInfo { get; set; }
        /// <summary>
        /// A string to prepend to all PRIVMSGs sent. Many IRC bots prefix their messages with \u200B, to
        /// indicate to other bots that you are a bot.
        /// </summary>
        public string PrivmsgPrefix { get; set; }
        /// <summary>
        /// A list of users on this network that we are aware of.
        /// </summary>
        public UserPool Users { get; set; }
        /// <summary>
        /// A list of capabilities supported by the library, along with enabled and disabled capabilities
        /// after negotiating with the server.
        /// </summary>
        public CapabilityPool Capabilities { get; set; }
        /// <summary>
        /// Set to true when the client is negotiating IRC capabilities with the server.
        /// If set to False, capability negotiation is finished.
        /// </summary>
        public bool IsNegotiatingCapabilities { get; internal set; }
        /// <summary>
        /// Set to True when the client is authenticating with SASL.
        /// If set to False, SASL authentication is finished.
        /// </summary>
        public bool IsAuthenticatingSasl { get; internal set; }

        private Task StreamReader { get; set; }
        private Pipe Pipe { get; set; }
        public object NamedEventsLock = new object();
        public Dictionary<string, AsyncManualResetEvent> NamedEvents { get; internal set; } = new Dictionary<string, AsyncManualResetEvent>();
        private SemaphoreSlim WriteLock = new SemaphoreSlim(1, 1);

        private static readonly Channel<string> RW = Channel.CreateUnbounded<string>(new UnboundedChannelOptions() {
            AllowSynchronousContinuations = true,
            SingleReader = true,
            SingleWriter = false
        });

        /// <summary>
        /// Creates a new IRC client, but will not connect until ConnectAsync is called.
        /// </summary>
        /// <param name="serverAddress">Server address including port in the form of "hostname:port".</param>
        /// <param name="user">The IRC user to connect as.</param>
        /// <param name="useSSL">Connect with SSL if true.</param>
        public IrcClient(string serverAddress, IrcUser user, bool useSSL = false)
        {
            if (serverAddress == null) throw new ArgumentNullException("serverAddress");
            if (user == null) throw new ArgumentNullException("user");

            User = user;
            ServerAddress = serverAddress;
            Encoding = Encoding.UTF8;
            Settings = new ClientSettings();
            Handlers = new Dictionary<string, MessageHandler>();
            MessageHandlers.RegisterDefaultHandlers(this);
            RequestManager = new RequestManager();
            UseSSL = useSSL;
            ServerInfo = new ServerInfo();
            PrivmsgPrefix = "";
            Channels = User.Channels = new ChannelCollection(this);
            Users = new UserPool {
                User // Add self to user pool
            };
            Capabilities = new CapabilityPool();

            // List of supported capabilities
            Capabilities.AddRange(new string[] {
                "server-time", "multi-prefix", "cap-notify", "znc.in/server-time", "znc.in/server-time-iso",
                "account-notify", "chghost", "userhost-in-names", "sasl", "oragono.io/maxline-2=32767"
            });

            IsNegotiatingCapabilities = false;
            IsAuthenticatingSasl = false;

            RandomNumber = new Random();
        }

        public async ValueTask<bool> WaitForNamedEvent(string Name)
        {
            AsyncManualResetEvent ev;
            lock (NamedEventsLock)
            {
                if (!NamedEvents.TryGetValue(Name, out ev))
                {
                    return false;
                }
            }
            var cancellation = new CancellationTokenSource();
            var task = ev.WaitAsync(cancellation.Token);
            await Task.WhenAny(Task.Delay(TimeSpan.FromMinutes(1)), task);
            try { cancellation.Cancel(); } catch { }
            return true;
        }

        public void AddNamedEvent(string Name)
        {
            AsyncManualResetEvent ev;
            lock (NamedEventsLock)
            {
                if (!NamedEvents.TryGetValue(Name, out ev))
                {
                    ev = new AsyncManualResetEvent(false);
                    NamedEvents[Name] = ev;
                }
            }

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(1));
                ev.Set();
                lock (NamedEventsLock)
                {
                    NamedEvents.Remove(Name);
                }
            });
        }

        public void CompleteNamedEvent(string Name)
        {
            lock (NamedEventsLock)
            {
                if (NamedEvents.TryGetValue(Name, out var ev))
                {
                    ev.Set();
                }
            }
        }

        /// <summary>
        /// Connects to the IRC server.
        /// </summary>
        public async Task ConnectAsync(bool AuthenticateLegacy)
        {
            if (TcpClient != null && TcpClient.Connected) throw new InvalidOperationException("Socket is already connected to server.");
            TcpClient = new TcpClient();
            PingTimer = new System.Timers.Timer(30000);
            PingTimer.Elapsed += async (sender, e) => 
            {
                if (!string.IsNullOrEmpty(ServerNameFromPing))
                    await SendRawMessage("PING :{0}", ServerNameFromPing);
            };
            await TcpClient.ConnectAsync(ServerHostname, ServerPort);

            try
            {
                NetworkStream = TcpClient.GetStream();
                if (UseSSL)
                {
                    if (IgnoreInvalidSSL)
                        NetworkStream = new SslStream(NetworkStream, false, (sender, certificate, chain, policyErrors) => true);
                    else
                        NetworkStream = new SslStream(NetworkStream);
                    ((SslStream)NetworkStream).AuthenticateAsClient(ServerHostname);
                }

                Pipe = new Pipe();
                StreamReader = Task.WhenAll(FillPipe(), ReadPipe(), RWConsumer());

                // Begin capability negotiation
                await SendRawMessage("CAP LS 302");
                // Write login info
                if (AuthenticateLegacy && !string.IsNullOrEmpty(User.Password))
                    await SendRawMessage("PASS {0}", User.Password);
                await SendRawMessage("NICK {0}", User.Nick);
                // hostname, servername are ignored by most IRC servers
                await SendRawMessage("USER {0} hostname servername :{1}", User.User, User.RealName);
                PingTimer.Start();
            }
            catch (SocketException e)
            {
                OnNetworkError(new SocketErrorEventArgs(e.SocketErrorCode));
            }
            catch (Exception e)
            {
                OnError(new Events.ErrorEventArgs(e));
            }
        }

        /// <summary>
        /// Send a QUIT message and disconnect.
        /// </summary>
        public ValueTask Quit() => Quit(null);

        /// <summary>
        /// Send a QUIT message with a reason and disconnect.
        /// </summary>
        public async ValueTask Quit(string reason)
        {
            if (reason == null)
                await SendRawMessage("QUIT");
            else
                await SendRawMessage("QUIT :{0}", reason);

            try { TcpClient.Close(); } catch { }
            TcpClient.Dispose();
            TcpClient = null;

            StreamReader = null;
            try { Pipe.Writer.Complete(); } catch { }
            try { Pipe.Reader.Complete(); } catch { }
            try { RW.Writer.Complete(); } catch { }
            Pipe = null;

            PingTimer.Dispose();
        }

        private async Task RWConsumer()
        {
            while (await RW.Reader.WaitToReadAsync()) {
                if (!RW.Reader.TryRead(out var item))
                    continue;

                try { HandleMessage(item); } catch { }
            }
        }

        private async Task FillPipe()
        {
            const int minimumBufferSize = 512;

            while (true)
            {
                var memory = Pipe.Writer.GetMemory(minimumBufferSize);
                try
                {
                    int bytesRead = await NetworkStream.ReadAsync(memory);
                    if (bytesRead == 0)
                    {
                        OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
                        return;
                    }
                    Pipe.Writer.Advance(bytesRead);
                }
                catch (IOException ex)
                {
                    if (ex.InnerException is SocketException socketException)
                        OnNetworkError(new SocketErrorEventArgs(socketException.SocketErrorCode));
                    else {
                        OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
                        throw;
                    }
                }

                var result = await Pipe.Writer.FlushAsync();

                if (result.IsCompleted)
                {
                    OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
                    break;
                }
            }

            Pipe.Writer.Complete();
        }

        private async Task ReadPipe()
        {
            while (true)
            {
                var result = await Pipe.Reader.ReadAsync();

                var buffer = result.Buffer;
                SequencePosition? position;
                do
                {
                    // Look for a EOL in the buffer
                    position = buffer.PositionOf((byte)'\n');

                    if (position != null)
                    {
                        var buffSlice = buffer.Slice(0, position.Value);
                        string msg;

                        if (buffSlice.IsSingleSegment)
                        {
                            if (buffSlice.First.Span[^1] == (byte)'\r')
                                msg = Encoding.UTF8.GetString(buffSlice.First.Span.Slice(0, buffSlice.First.Span.Length - 1));
                            else
                                msg = Encoding.UTF8.GetString(buffSlice.First.Span);
                        }
                        else
                        {
                            var len = (int)buffSlice.Length;

                            var pos = buffSlice.GetPosition(buffSlice.Length - 1);
                            ReadOnlyMemory<byte> byt = new byte[1];
                            buffSlice.TryGet(ref pos, out byt, false);

                            if (byt.Span[0] == (byte)'\r')
                            {
                                len--;
                                buffSlice = buffSlice.Slice(0, len);
                            }

                            msg = String.Create(len, buffSlice, (span, sequence) => {
                                foreach (var segment in sequence)
                                {
                                    Encoding.UTF8.GetChars(segment.Span, span);

                                    span = span.Slice(segment.Length);
                                }
                            });
                        }

                        await RW.Writer.WriteAsync(msg);

                        // Skip the line + the \n character (basically position)
                        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                    }
                }
                while (position != null);

                // Tell the PipeReader how much of the buffer we have consumed
                Pipe.Reader.AdvanceTo(buffer.Start, buffer.End);

                // Stop reading if there's no more data coming
                if (result.IsCompleted)
                {
                    OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
                    break;
                }
            }

            // Mark the PipeReader as complete
            Pipe.Reader.Complete();
        }

        private void HandleMessage(string rawMessage)
        {
            OnRawMessageRecieved(new RawMessageEventArgs(rawMessage, false));
            var message = new IrcMessage(rawMessage);
            if (Handlers.ContainsKey(message.Command.ToUpper()))
                Handlers[message.Command.ToUpper()](this, message);
            else
            {
                // TODO: Fire an event or something
            }
        }

        /// <summary>
        /// Send a raw IRC message. Behaves like /quote in most IRC clients.
        /// </summary>
        public async ValueTask SendRawMessage(string message, params object[] format)
        {
            if (NetworkStream == null)
            {
                OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
                return;
            }

            message = string.Format(message, format);
            var data = Encoding.GetBytes(message + "\r\n");

            if (NetworkStream == null)
            {
                OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
                return;
            }

            try
            {
                await WriteLock.WaitAsync();
                try {
                    await NetworkStream.WriteAsync(data, 0, data.Length);
                } finally {
                    WriteLock.Release();
                }
            }
            catch (IOException e) when (e.InnerException is SocketException socketException)
            {
                OnNetworkError(new SocketErrorEventArgs(socketException.SocketErrorCode));
                return;
            }
            catch (IOException e)
            {
                OnNetworkError(new SocketErrorEventArgs(SocketError.NotConnected));
            }

            OnRawMessageSent(new RawMessageEventArgs(message, true));
        }

        /// <summary>
        /// Send a raw IRC message. Behaves like /quote in most IRC clients.
        /// </summary>
        public ValueTask SendIrcMessage(IrcMessage message) => SendRawMessage(message.RawMessage);

        /// <summary>
        /// IRC Error Replies. rfc1459 6.1.
        /// </summary>
        public event EventHandler<Events.ErrorReplyEventArgs> ErrorReply;
        internal void OnErrorReply(Events.ErrorReplyEventArgs e)
        {
            ErrorReply?.Invoke(this, e);
        }
        /// <summary>
        /// Raised for errors.
        /// </summary>
        public event EventHandler<Events.ErrorEventArgs> Error;
        internal void OnError(Events.ErrorEventArgs e)
        {
            Error?.Invoke(this, e);
        }
        /// <summary>
        /// Raised for socket errors. ChatSharp does not automatically reconnect.
        /// </summary>
        public event EventHandler<SocketErrorEventArgs> NetworkError;
        internal void OnNetworkError(SocketErrorEventArgs e)
        {
            NetworkError?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a raw message is sent.
        /// </summary>
        public event EventHandler<RawMessageEventArgs> RawMessageSent;
        internal void OnRawMessageSent(RawMessageEventArgs e)
        {
            RawMessageSent?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a raw message recieved.
        /// </summary>
        public event EventHandler<RawMessageEventArgs> RawMessageRecieved;
        internal void OnRawMessageRecieved(RawMessageEventArgs e)
        {
            RawMessageRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a notice recieved.
        /// </summary>
        public event EventHandler<IrcNoticeEventArgs> NoticeRecieved;
        internal void OnNoticeRecieved(IrcNoticeEventArgs e)
        {
            NoticeRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when the server has sent us part of the MOTD.
        /// </summary>
        public event EventHandler<ServerMOTDEventArgs> MOTDPartRecieved;
        internal void OnMOTDPartRecieved(ServerMOTDEventArgs e)
        {
            MOTDPartRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when the entire server MOTD has been recieved.
        /// </summary>
        public event EventHandler<ServerMOTDEventArgs> MOTDRecieved;
        internal void OnMOTDRecieved(ServerMOTDEventArgs e)
        {
            MOTDRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a private message recieved. This can be a channel OR a user message.
        /// </summary>
        public event EventHandler<PrivateMessageEventArgs> PrivateMessageRecieved;
        internal void OnPrivateMessageRecieved(PrivateMessageEventArgs e)
        {
            PrivateMessageRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a message is recieved in an IRC channel.
        /// </summary>
        public event EventHandler<PrivateMessageEventArgs> ChannelMessageRecieved;
        internal void OnChannelMessageRecieved(PrivateMessageEventArgs e)
        {
            ChannelMessageRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a message is recieved from a user.
        /// </summary>
        public event EventHandler<PrivateMessageEventArgs> UserMessageRecieved;
        internal void OnUserMessageRecieved(PrivateMessageEventArgs e)
        {
            UserMessageRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Raised if the nick you've chosen is in use. By default, ChatSharp will pick a
        /// random nick to use instead. Set ErronousNickEventArgs.DoNotHandle to prevent this.
        /// </summary>
        public event EventHandler<ErronousNickEventArgs> NickInUse;
        internal void OnNickInUse(ErronousNickEventArgs e)
        {
            NickInUse?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a user or channel mode is changed.
        /// </summary>
        public event EventHandler<ModeChangeEventArgs> ModeChanged;
        internal void OnModeChanged(ModeChangeEventArgs e)
        {
            ModeChanged?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a user joins a channel.
        /// </summary>
        public event EventHandler<ChannelUserEventArgs> UserJoinedChannel;
        internal void OnUserJoinedChannel(ChannelUserEventArgs e)
        {
            UserJoinedChannel?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a user parts a channel.
        /// </summary>
        public event EventHandler<ChannelUserEventArgs> UserPartedChannel;
        internal void OnUserPartedChannel(ChannelUserEventArgs e)
        {
            UserPartedChannel?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when we have received the list of users present in a channel.
        /// </summary>
        public event EventHandler<ChannelEventArgs> ChannelListRecieved;
        internal void OnChannelListRecieved(ChannelEventArgs e)
        {
            ChannelListRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when we have received the topic of a channel.
        /// </summary>
        public event EventHandler<ChannelTopicEventArgs> ChannelTopicReceived;
        internal void OnChannelTopicReceived(ChannelTopicEventArgs e)
        {
            ChannelTopicReceived?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when the IRC connection is established and it is safe to begin interacting with the server.
        /// </summary>
        public event EventHandler<EventArgs> ConnectionComplete;
        internal void OnConnectionComplete(EventArgs e)
        {
            ConnectionComplete?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when we receive server info (such as max nick length).
        /// </summary>
        public event EventHandler<SupportsEventArgs> ServerInfoRecieved;
        internal void OnServerInfoRecieved(SupportsEventArgs e)
        {
            ServerInfoRecieved?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a user is kicked.
        /// </summary>
        public event EventHandler<KickEventArgs> UserKicked;
        internal void OnUserKicked(KickEventArgs e)
        {
            UserKicked?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a WHOIS response is received.
        /// </summary>
        public event EventHandler<WhoIsReceivedEventArgs> WhoIsReceived;
        internal void OnWhoIsReceived(WhoIsReceivedEventArgs e)
        {
            WhoIsReceived?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a user has changed their nick.
        /// </summary>
        public event EventHandler<NickChangedEventArgs> NickChanged;
        internal void OnNickChanged(NickChangedEventArgs e)
        {
            NickChanged?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a user has quit.
        /// </summary>
        public event EventHandler<UserEventArgs> UserQuit;
        internal void OnUserQuit(UserEventArgs e)
        {
            UserQuit?.Invoke(this, e);
        }
        /// <summary>
        /// Occurs when a WHO (WHOX protocol) is received.
        /// </summary>
        public event EventHandler<WhoxReceivedEventArgs> WhoxReceived;
        internal void OnWhoxReceived(WhoxReceivedEventArgs e)
        {
            WhoxReceived?.Invoke(this, e);
        }
    }
}
