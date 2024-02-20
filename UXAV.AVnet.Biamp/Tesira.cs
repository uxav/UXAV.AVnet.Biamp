using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UXAV.AVnet.Biamp.ControlBlocks;
using UXAV.AVnet.Core.DeviceSupport;
using UXAV.AVnet.Core.Models.Diagnostics;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public class Tesira : DeviceBase, IFusionAsset, IEnumerable<TesiraBlockBase>
    {
        private readonly TtpSshClient _client;

        public Tesira(string name, string address, string username = "default", string password = "default",
            uint roomIdAllocated = 0)
            : base(name, roomIdAllocated)
        {
            _client = new TtpSshClient(address, username, password);
            _client.ReceivedData += ClientOnReceivedData;
            _client.ConnectionStatusChange += ClientOnConnectionStatusChange;
        }

        public TesiraBlockBase this[string instanceId] => Controls[instanceId];

        public override string ConnectionInfo => _client.DeviceAddress;

        public override string VersionInfo { get; } = "Unknown";

        internal Dictionary<string, TesiraBlockBase> Controls { get; } = new Dictionary<string, TesiraBlockBase>();

        public IEnumerator<TesiraBlockBase> GetEnumerator()
        {
            return Controls.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ManufacturerName => "Biamp";

        public override string ModelName { get; } = "Tesira";

        public override string SerialNumber { get; } = "Unknown";

        public override string Identity => "";

        public FusionAssetType FusionAssetType => FusionAssetType.AudioProcessor;

        public event TtpSshClientReceivedDataEventHandler ReceivedData;

        internal static string FormatBaseMessage(string instanceTag, TesiraCommand command,
            TesiraAttributeCode attributeCode)
        {
            return $"{instanceTag} {command.ToString().ToLower()} {attributeCode.ToCommandString()}";
        }

        internal static string FormatBaseMessage(string instanceTag, TesiraCommand command)
        {
            return $"{instanceTag} {command.ToCommandString()}";
        }

        public void Send(string data)
        {
            _client.Send(data);
        }

        public void Send(string instanceTag, TesiraCommand command, TesiraAttributeCode attributeCode)
        {
            _client.Send(FormatBaseMessage(instanceTag, command, attributeCode));
        }

        public void Send(string instanceTag, TesiraCommand command, TesiraAttributeCode attributeCode, uint[] indexes)
        {
            var message = FormatBaseMessage(instanceTag, command, attributeCode);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, TesiraAttributeCode attributeCode, uint[] indexes,
            string value)
        {
            var message = FormatBaseMessage(instanceTag, command, attributeCode);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " \"" + value + "\"";
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, TesiraAttributeCode attributeCode, uint[] indexes,
            int value)
        {
            var message = FormatBaseMessage(instanceTag, command, attributeCode);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " " + value;
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, TesiraAttributeCode attributeCode, uint[] indexes,
            double value)
        {
            var message = FormatBaseMessage(instanceTag, command, attributeCode);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " " + value.ToString("F1");
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, TesiraAttributeCode attributeCode, uint[] indexes,
            bool value)
        {
            var message = FormatBaseMessage(instanceTag, command, attributeCode);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " " + value.ToString().ToLower();
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, uint[] indexes)
        {
            var message = FormatBaseMessage(instanceTag, command);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, uint[] indexes, string value)
        {
            var message = FormatBaseMessage(instanceTag, command);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " \"" + value + "\"";
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, uint[] indexes, int value)
        {
            var message = FormatBaseMessage(instanceTag, command);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " " + value;
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, uint[] indexes, double value)
        {
            var message = FormatBaseMessage(instanceTag, command);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " " + value.ToString("F1");
            _client.Send(message);
        }

        public void Send(string instanceTag, TesiraCommand command, uint[] indexes, bool value)
        {
            var message = FormatBaseMessage(instanceTag, command);
            message = indexes.Aggregate(message, (current, index) => current + " " + index);
            message = message + " " + value.ToString().ToLower();
            _client.Send(message);
        }

        public void RecallPresetByName(string name)
        {
            Send($"DEVICE recallPresetByName \"{name}\"");
        }

        private void ClientOnConnectionStatusChange(TtpSshClient client, TtpSshClient.ClientStatus status)
        {
            switch (status)
            {
                case TtpSshClient.ClientStatus.Connected:
                    Send("DEVICE", TesiraCommand.Get, TesiraAttributeCode.NetworkStatus);
                    Send("SESSION", TesiraCommand.Get, TesiraAttributeCode.Aliases);
                    DeviceCommunicating = true;
                    break;
                case TtpSshClient.ClientStatus.Disconnected:
                    DeviceCommunicating = false;
                    break;
            }
        }

        private void ClientOnReceivedData(TtpSshClient client, TesiraMessage message)
        {
            try
            {
                JToken json;
                switch (message.Type)
                {
                    case TesiraMessageType.OkWithResponse:
                        var response = message as TesiraResponse;
                        if (response == null) return;
                        if (response.Command == "DEVICE get networkStatus")
                        {
                            json = response.TryParseResponse();
                            if (json != null)
                                Logger.Debug("Network Config" + "\r\n" + json.ToString(Formatting.Indented));
                        }
                        else if (response.Command == "SESSION get aliases")
                        {
                            json = response.TryParseResponse();
                            if (json != null)
                                Logger.Debug("Aliases found:" + "\r\n" + json.ToString(Formatting.Indented));
                        }
                        break;
                    case TesiraMessageType.Notification:
                        // json = message.TryParseResponse();
                        // if (json != null) Logger.Debug(json.ToString(Formatting.Indented));
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            if (ReceivedData == null) return;
            try
            {
                ReceivedData(client, message);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        public TesiraBlockBase RegisterControlBlock(TesiraBlockType type, string instanceId)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var nameSpace = typeof(LevelControlBlock).Namespace;
            var blockType = assembly.GetType($@"{nameSpace}.{type}");
            var ctor = blockType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(Tesira), typeof(string) }, null);
            return (TesiraBlockBase)ctor.Invoke(new object[] { this, instanceId });
        }

        public bool HasControlWithInstanceId(string instanceId)
        {
            return Controls.ContainsKey(instanceId);
        }

        public override IEnumerable<DiagnosticMessage> GetMessages()
        {
            return DeviceCommunicating ? new[] { this.CreateOnlineMessage() } : new[] { this.CreateOfflineMessage() };
        }

        public override void Initialize()
        {
            _client.Connect();
        }

        protected override void OnProgramStopping()
        {
            _client.Disconnect();
        }

        internal static string FixJsonData(string jsonData)
        {
#if DEBUG
            Logger.Debug("Trying to fix json with Regex");
#endif
            var fix = Regex.Replace(jsonData, @""":(?![:\d])(?!true|false)([\w]+)", @""":""$1""");
            fix = Regex.Replace(fix, @"(""|: *[\d\-\.]+|: *null|}|\]|: *true|: *false) ([""{\[])(?![:,}])", @"$1,$2");
            fix = Regex.Replace(fix, @"(\w)? ([\w\-\.])", @"$1,$2");
            fix = "{" + fix + "}";
#if DEBUG
            Logger.Debug($"Fixed json: {fix}");
#endif
            return fix;
        }
    }

    public static class TesiraExtensions
    {
        public static string ToCommandString(this TesiraAttributeCode attribute)
        {
            var str = attribute.ToString();
            return str.Substring(0, 1).ToLower() + str.Substring(1, str.Length - 1);
        }

        public static string ToCommandString(this TesiraCommand command)
        {
            var str = command.ToString();
            return str.Substring(0, 1).ToLower() + str.Substring(1, str.Length - 1);
        }
    }

    public enum TesiraCommand
    {
        Get,
        Set,
        Increment,
        Decrement,
        Toggle,
        Subscribe,
        Unsubscribe,
        Dial,
        OnHook,
        OffHook,
        End,
        Answer,
        RecallPreset,
        RecallPresetByName
    }

    public enum TesiraAttributeCode
    {
        Unknown,
        Verbose,
        Aliases,
        Ganged,
        Label,
        Level,
        Levels,
        MaxLevel,
        MinLevel,
        Mute,
        Mutes,
        NumChannels,
        RampInterval,
        RampStep,
        UseRamping,
        NetworkStatus,
        CallState,
        DisplayNameLabel,
        LineLabel,
        State,
        NumInputs,
        NumOutputs,
        NumSources,
        SourceSelection,
        StereoEnable,
        Gain,
        ChannelName
    }
}