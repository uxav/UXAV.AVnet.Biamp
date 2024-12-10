using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Crestron.SimplSharp;
using Renci.SshNet;
using Renci.SshNet.Common;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public class TtpSshClient
    {
        private SshClient _client;
        private bool _tryDefaultLogin;
        private bool _reconnect;
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _requestsSent = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _requestsAwaiting = new ConcurrentQueue<string>();
        private readonly string _address;
        private readonly string _username;
        private readonly string _password;
        private bool _programRunning = true;
        private ClientStatus _connectionStatus;
        private ShellStream _shell;
        private decimal _timeOutCount;
        private readonly AutoResetEvent _retryWait = new AutoResetEvent(false);
        private const int BufferSize = 100000;
        private const long KeepAliveTime = 30000;

        public TtpSshClient(string address, string username, string password)
        {
            _address = address;
            _username = username;
            _password = password;
            CrestronEnvironment.ProgramStatusEventHandler += type =>
            {
                _programRunning = type != eProgramStatusEventType.Stopping;
                if (!_programRunning && Connected)
                {
                    _reconnect = false;
                    _shell?.Dispose();
                }
            };
        }


        public event TtpSshClientReceivedDataEventHandler ReceivedData;

        public event TtpSshClientConnectionStatusChangedHandler ConnectionStatusChange;

        public enum ClientStatus
        {
            Disconnected,
            AttemptingConnection,
            Connected
        }

        public string DeviceAddress
        {
            get { return _address; }
        }

        public bool Connected
        {
            get { return _connectionStatus == ClientStatus.Connected; }
        }

        public ClientStatus ConnectionStatus
        {
            get { return _connectionStatus; }
            private set
            {
                if (_connectionStatus == value) return;
                var previousValue = _connectionStatus;
                _connectionStatus = value;
                if (!(previousValue == ClientStatus.AttemptingConnection &&
                      _connectionStatus == ClientStatus.Disconnected))
                {
                    OnConnectionStatusChange(this, value);
                }
            }
        }

        private SshClient CreateClient(string host, string username, string password)
        {
            var keyboardAuthMethod = new KeyboardInteractiveAuthenticationMethod(username);
            keyboardAuthMethod.AuthenticationPrompt += (sender, e) =>
            {
                foreach (var prompt in e.Prompts)
                {
                    Logger.Debug("Tesira Prompt: {0}\rReturning password...", prompt.Request);
                    prompt.Response = password;
                }
            };

            var authMethods = new AuthenticationMethod[]
            {
                new PasswordAuthenticationMethod(username, password),
                keyboardAuthMethod
            };

            var connectionInfo = new ConnectionInfo(host, username, authMethods);
            return new SshClient(connectionInfo)
            {
                KeepAliveInterval = TimeSpan.FromMilliseconds(KeepAliveTime)
            };
        }

        public void Connect()
        {
            if (_client != null && _client.IsConnected)
            {
                Logger.Warn("Already connected");
                return;
            }

            _reconnect = true;

            _client = CreateClient(_address, _username, _password);
            _client.ErrorOccurred += (sender, args) => Logger.Error("Tesira SSh ErrorOccurred: {0}", args.Exception.Message);
            _client.HostKeyReceived +=
                (sender, args) =>
                {
                    Logger.Log($"Host key received for {_address}: {args.FingerPrintMD5}");
                    if (!args.CanTrust)
                    {
                        Logger.Warn("Host key not trusted for {_address}");
                        Logger.Debug("Setting CanTrust to true");
                        args.CanTrust = true;
                    }
                };

            Task.Run(SshCommsProcess);
        }

        public void Disconnect()
        {
            _retryWait.Set();
            if (_client == null || !_client.IsConnected)
            {
                Logger.Warn("Not connected");
                return;
            }

            _reconnect = false;
            _client.Disconnect();
        }

        public void Send(string line)
        {
            if (Connected)
            {
                _sendQueue.Enqueue(line);
            }
            else
            {
                Logger.Warn("Could not send \"{0}\" to Tesira. No Connection", line);
            }
        }

        protected virtual void OnConnectionStatusChange(TtpSshClient client, ClientStatus status)
        {
            var handler = ConnectionStatusChange;
            if (handler != null)
            {
                try
                {
                    handler(client, status);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        private async Task SshCommsProcess()
        {
            try
            {
                Thread.Sleep(1000);

                Logger.Debug($"{GetType().Name} attempting connection to {_address}");

                var firstFail = false;

                while (!_client.IsConnected && _reconnect)
                {
                    try
                    {
                        ConnectionStatus = ClientStatus.AttemptingConnection;
                        await _client.ConnectAsync(CancellationToken.None);
                    }
                    catch (SshAuthenticationException e)
                    {
                        Logger.Error($"Authentication failed for Tesira at {_address}, {e.Message}");
                        ConnectionStatus = ClientStatus.Disconnected;
                        _client.Dispose();
                        _client = null;
                        _tryDefaultLogin = !_tryDefaultLogin;
                        if (_tryDefaultLogin)
                        {
                            Logger.Warn($"Tesira {_address}, Attempting default login on next connect...");
                            _client = CreateClient(_address, "default", "");
                            if (_retryWait.WaitOne(TimeSpan.FromSeconds(5)))
                            {
                                _client.Dispose();
                                _client = null;
                                return;
                            }
                        }
                        else
                        {
                            Connect();
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        ConnectionStatus = ClientStatus.Disconnected;
                        if (!firstFail)
                        {
                            Logger.Error(e);
                            if (e.InnerException != null)
                            {
                                Logger.Error(e.InnerException);
                            }
                            firstFail = true;
                        }

                        if (_retryWait.WaitOne(TimeSpan.FromSeconds(30)))
                        {
                            Logger.Log("Aborting reconnection attempt");
                            _reconnect = false;
                            _client.Dispose();
                            _client = null;
                            return;
                        }
                    }
                }

                if (!_client.IsConnected && !_reconnect)
                {
                    _client.Dispose();
                    _client = null;
                    ConnectionStatus = ClientStatus.Disconnected;
                    return;
                }

                Logger.Success($"Connected to {_address}");

                _shell = _client.CreateShellStream("terminal", 80, 24, 800, 600, BufferSize);

                _ = Task.Run(() => ReadShell(_shell));

                try
                {
                    while (_programRunning && _client.IsConnected)
                    {
                        if (!_programRunning || !_client.IsConnected) break;

                        if (_shell.CanWrite && !_sendQueue.IsEmpty && _requestsSent.IsEmpty && _requestsAwaiting.IsEmpty)
                        {
                            _sendQueue.TryDequeue(out var s);
#if DEBUG
                            Logger.Debug($"Tesira Tx: {s}");
#endif
                            _timeOutCount = 0;
                            _shell.WriteLine(s);
                            _requestsSent.Enqueue(s);
                            Thread.Sleep(20);
                        }
                        else if (!_requestsSent.IsEmpty || !_requestsAwaiting.IsEmpty)
                        {
                            _timeOutCount++;

                            if (_timeOutCount > 100)
                            {
                                Logger.Warn(
                                    $"Error waiting to send requests, _requestsAwaiting.Count = {_requestsAwaiting.Count}" +
                                    $" and _requestsSent.Count = {_requestsSent.Count}. Clearing queues!");
                                while (_requestsAwaiting.TryDequeue(out _)) { }
                                while (_requestsSent.TryDequeue(out _)) { }
                                _timeOutCount = 0;
                            }

                            Thread.Sleep(20);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                if (_client != null && _client.IsConnected)
                {
                    _client.Dispose();
                    _client = null;
                }

                Logger.Warn($"Disconnected from {_address}");
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            ConnectionStatus = ClientStatus.Disconnected;

            if (!_reconnect || !_programRunning)
            {
                return;
            }

            Thread.Sleep(1000);

            Logger.Log($"Attempting reconnect to Tesira at {_address}");
            ConnectionStatus = ClientStatus.AttemptingConnection;

            Connect();

            return;
        }

        async Task ReadShell(ShellStream shell)
        {
            var reader = new StreamReader(shell);

            while (true)
            {
                var line = await reader.ReadLineAsync();

                if (line == null)
                {
                    Logger.Warn("Tesira Shell stream closed!");
                    return;
                }

                if (!Connected && line.Contains("Welcome to the Tesira Text Protocol Server..."))
                {
                    while (_requestsAwaiting.TryDequeue(out _)) { }
                    while (_requestsSent.TryDequeue(out _)) { }
                    while (_sendQueue.TryDequeue(out _)) { }
                    _sendQueue.Enqueue("SESSION set verbose true");
                    ConnectionStatus = ClientStatus.Connected;
                }

#if DEBUG
                Logger.Debug($"Tesira Rx Line: {line}");
#endif
                TesiraMessage message = null;

                if (line == "+OK")
                {
                    if (_requestsAwaiting.TryDequeue(out var request))
                    {
#if DEBUG
                        Logger.Debug($"Request Response Received: {request}");
                        Logger.Success(line);
#endif
                        message = new TesiraResponse(request, null);
                    }
                }
                else if (line.StartsWith("+OK "))
                {
                    if (_requestsAwaiting.TryDequeue(out var request))
                    {
#if DEBUG
                        Logger.Debug($"Request Response Received: {request}");
                        Logger.Success(line);
#endif
                        message = new TesiraResponse(request, line.Substring(4));
                    }
                }
                else if (line.StartsWith("-ERR "))
                {
                    if (_requestsAwaiting.TryDequeue(out var request))
                    {
#if DEBUG
                        Logger.Debug($"Request Response Received: {request}");
                        Logger.Error(line);
#endif
                        message = new TesiraErrorResponse(request, line.Substring(5));
                    }
                    else
                    {
                        Logger.Debug("Error received and request queue returned null!");
                        Logger.Error(line);
                        while (_requestsSent.TryDequeue(out _)) { }
                        while (_requestsAwaiting.TryDequeue(out _)) { }
                    }
                }
                else if (line.StartsWith("! "))
                {
#if DEBUG
                    Logger.Debug("Notification Received");
                    Logger.Debug(line);
#endif
                    message = new TesiraNotification(line.Substring(2));
                }
                else if (!_requestsSent.IsEmpty)
                {
                    if (_requestsSent.TryPeek(out var lastSent))
                    {
#if DEBUG
                        Logger.Debug($"Last sent request: {lastSent}");
#endif
                        if (lastSent == line)
                        {
                            if (_requestsSent.TryDequeue(out lastSent))
                            {
                                _requestsAwaiting.Enqueue(lastSent);
#if DEBUG
                                Logger.Debug($"Now awaiting for response for command: {line}");
#endif
                            }
                        }
                    }
                }

                if (message != null && ReceivedData != null && message.Type != TesiraMessageType.ErrorResponse)
                {
                    if (ReceivedData == null) continue;
                    try
                    {
                        _timeOutCount = 0;

                        ReceivedData(this, message);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                    }
                }
                else if (message != null && message.Type == TesiraMessageType.ErrorResponse)
                {
                    _timeOutCount = 0;

                    Logger.Error($"Error message from Tesira: \"{message.Message}\"");
                }
            }
        }

        public delegate void TtpSshClientConnectionStatusChangedHandler(TtpSshClient client, TtpSshClient.ClientStatus status);
        public delegate void TtpSshClientReceivedDataEventHandler(TtpSshClient client, TesiraMessage message);
    }
}