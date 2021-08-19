using System;
using System.Linq;
using System.Text;
using System.Threading;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Ssh;
using Crestron.SimplSharp.Ssh.Common;
using UXAV.Logging;
using Stopwatch = Crestron.SimplSharp.Stopwatch;
using Thread = Crestron.SimplSharpPro.CrestronThread.Thread;

namespace UXAV.AVnet.Biamp
{
    public class TtpSshClient
    {
        private SshClient _client;
        private Thread _sshProcess;
        private bool _reconnect;
        private readonly CrestronQueue<string> _sendQueue = new CrestronQueue<string>(500);
        private readonly CrestronQueue<string> _requestsSent = new CrestronQueue<string>(500);
        private readonly CrestronQueue<string> _requestsAwaiting = new CrestronQueue<string>(500);
        private readonly string _address;
        private readonly string _username;
        private readonly string _password;
        private bool _programRunning = true;
#if DEBUG
        private readonly Stopwatch _stopWatch = new Stopwatch();
#endif
        private ClientStatus _connectionStatus;
        private ShellStream _shell;
        private CTimer _keepAliveTimer;
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
                    Send("bye");
                }
                //if (!_programRunning && _threadWait != null)
                //_threadWait.Set();
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

        public void Connect()
        {
            if (_client != null && _client.IsConnected)
            {
                Logger.Warn("Already connected");
                return;
            }

            _reconnect = true;

            var info = new KeyboardInteractiveConnectionInfo(_address, 22, _username);
            info.AuthenticationPrompt += OnPasswordPrompt;

            _client = new SshClient(info);
            _client.ErrorOccurred += (sender, args) => CrestronConsole.PrintLine("ErrorOccurred: {0}", args.Exception.Message);
            _client.HostKeyReceived +=
                (sender, args) => CrestronConsole.PrintLine("HostKeyReceived: {1}, can trust = {0}", args.CanTrust,
                    args.HostKeyName);

            _sshProcess = new Thread(SshCommsProcess, null, Thread.eThreadStartOptions.CreateSuspended)
            {
                Name = "Tesira SSH Comms Handler",
                Priority = Thread.eThreadPriority.HighPriority
            };

            _sshProcess.Start();
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

        private void OnPasswordPrompt(object sender, AuthenticationPromptEventArgs authenticationPromptEventArgs)
        {
            foreach (
                var prompt in
                    authenticationPromptEventArgs.Prompts.Where(prompt => prompt.Request.Contains("Password:")))
            {
                Logger.Debug("Tesira password prompt ... sending password");
                prompt.Response = _password;
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

        private object SshCommsProcess(object userSpecific)
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
                        _client.Connect();
                    }
                    catch
                    {
                        ConnectionStatus = ClientStatus.Disconnected;
                        if (!firstFail)
                        {
                            Logger.Warn("{0} could not connect to {1}, will retry every 30 seconds until connected",
                                GetType().Name, _address);
                            firstFail = true;
                        }

                        if (_retryWait.WaitOne(TimeSpan.FromSeconds(30)))
                        {
                            Logger.Log("Aborting reconnection attempt");
                            _reconnect = false;
                            _client.Dispose();
                            _client = null;
                            return null;
                        }
                    }
                }

                if (!_client.IsConnected && !_reconnect)
                {
                    _client.Dispose();
                    _client = null;
                    ConnectionStatus = ClientStatus.Disconnected;
                    return null;
                }

                Logger.Success($"Connected to {_address}");

                _shell = _client.CreateShellStream("terminal", 80, 24, 800, 600, BufferSize);

                var buffer = new byte[BufferSize];
                var dataCount = 0;

                try
                {
                    while (_programRunning && _client.IsConnected)
                    {

                        while (_shell.CanRead && _shell.DataAvailable)
                        {
                            var incomingData = new byte[BufferSize];
                            var incomingDataCount = _shell.Read(incomingData, 0, incomingData.Length);
#if DEBUG
                            _stopWatch.Start();
                            Logger.Debug($"Tesira rx {incomingDataCount} bytes");
                            //Debug.WriteNormal(Debug.AnsiBlue +
                            //                  Tools.GetBytesAsReadableString(incomingData, 0, incomingDataCount, true) +
                            //                  Debug.AnsiReset);
#endif
                            if (!Connected &&
                                Encoding.ASCII.GetString(incomingData, 0, incomingDataCount)
                                    .Contains("Welcome to the Tesira Text Protocol Server..."))
                            {
                                _requestsSent.Clear();
                                _requestsAwaiting.Clear();
                                _sendQueue.Enqueue("SESSION set verbose true");
                                ConnectionStatus = ClientStatus.Connected;
                                _keepAliveTimer = new CTimer(specific =>
                                {
#if DEBUG
                                    Logger.Debug("Sending KeepAlive");
#endif
                                    _client.SendKeepAlive();
                                }, null, KeepAliveTime, KeepAliveTime);
                            }
                            else if (Connected)
                            {
                                for (var i = 0; i < incomingDataCount; i++)
                                {
                                    buffer[dataCount] = incomingData[i];

                                    if (buffer[dataCount] == 10)
                                    {
                                        //skip
                                    }
                                    else if (buffer[dataCount] != 13)
                                    {
                                        dataCount++;
                                    }
                                    else
                                    {
                                        if(dataCount == 0) continue;

                                        var line = Encoding.UTF8.GetString(buffer, 0, dataCount);
                                        dataCount = 0;
#if DEBUG
                                        Logger.Debug($"Tesira Rx Line: {line}");
#endif
                                        TesiraMessage message = null;

                                        if (line == "+OK")
                                        {
                                            var request = _requestsAwaiting.TryToDequeue();
                                            if (request != null)
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
                                            var request = _requestsAwaiting.TryToDequeue();
                                            if (request != null)
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
                                            var request = _requestsAwaiting.TryToDequeue();
                                            if (request != null)
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
                                                _requestsSent.Clear();
                                                _requestsAwaiting.Clear();
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
                                            Logger.Debug($"Last sent request: {_requestsSent.Peek()}");

                                            if (_requestsSent.Peek() == line)
                                            {
                                                _requestsAwaiting.Enqueue(_requestsSent.Dequeue());
#if DEBUG
                                                Logger.Debug($"Now awaiting for response for command: {line}");
#endif
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
                            }
#if DEBUG
                            _stopWatch.Stop();
                            Logger.Debug($"Time to process: {_stopWatch.ElapsedMilliseconds} ms");
                            _stopWatch.Reset();
#endif
                            CrestronEnvironment.AllowOtherAppsToRun();
                        }

                        if (!_programRunning || !_client.IsConnected) break;
#if DEBUG
                        //Debug.WriteNormal(Debug.AnsiBlue +
                        //                  string.Format(
                        //                      "Shell Can Write = {0}, _sendQueue = {1}, _requestsSent = {2}, _requestsAwaiting = {3}",
                        //                      _shell.CanWrite, _sendQueue.Count, _requestsSent.Count,
                        //                      _requestsAwaiting.Count) + Debug.AnsiReset);
#endif
                        if (_shell.CanWrite && !_sendQueue.IsEmpty && _requestsSent.IsEmpty && _requestsAwaiting.IsEmpty)
                        {
                            var s = _sendQueue.Dequeue();

                            if (_keepAliveTimer != null && !_keepAliveTimer.Disposed)
                            {
                                _keepAliveTimer.Reset(KeepAliveTime, KeepAliveTime);
                            }
#if DEBUG
                            Logger.Debug($"Tesira Tx: {s}");
#endif
                            _timeOutCount = 0;
                            _shell.WriteLine(s);
                            _requestsSent.Enqueue(s);
                            Thread.Sleep(20);
                        }
                        else if(!_requestsSent.IsEmpty || !_requestsAwaiting.IsEmpty)
                        {
                            _timeOutCount ++;

                            if (_timeOutCount > 100)
                            {
                                Logger.Warn(
                                    $"Error waiting to send requests, _requestsAwaiting.Count = {_requestsAwaiting.Count}" +
                                    $" and _requestsSent.Count = {_requestsSent.Count}. Clearing queues!");
                                _requestsAwaiting.Clear();
                                _requestsSent.Clear();
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

                if (_keepAliveTimer != null && !_keepAliveTimer.Disposed)
                {
                    _keepAliveTimer.Stop();
                    _keepAliveTimer.Dispose();
                    _keepAliveTimer = null;
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
                return null;
            }

            Thread.Sleep(1000);

            Logger.Log($"Attempting reconnect to Tesira at {_address}");
            ConnectionStatus = ClientStatus.AttemptingConnection;

            Connect();

            return null;
        }
    }

    public delegate void TtpSshClientConnectionStatusChangedHandler(TtpSshClient client, TtpSshClient.ClientStatus status);
    public delegate void TtpSshClientReceivedDataEventHandler(TtpSshClient client, TesiraMessage message);
}