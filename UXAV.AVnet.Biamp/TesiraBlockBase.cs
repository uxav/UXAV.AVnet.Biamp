using System;
using System.Collections.Generic;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UXAV.AVnet.Core.DeviceSupport;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public abstract class TesiraBlockBase
    {
        private readonly Dictionary<string, TesiraAttributeCode> _subscriptions =
            new Dictionary<string, TesiraAttributeCode>();

        private CTimer _subscribeTimer;
        private string _name;

        protected TesiraBlockBase(Tesira device, string instanceTag)
        {
            Device = device;
            Device.DeviceCommunicatingChange += OnDeviceCommunicatingChange;
            Device.ReceivedData += OnReceivedData;
            InstanceTag = instanceTag;
            Device.Controls[InstanceTag] = this;
        }

        public Tesira Device { get; }

        public string InstanceTag { get; }

        public string Name
        {
            get => string.IsNullOrEmpty(_name) ? InstanceTag : _name;
            set => _name = value;
        }

        public abstract TesiraBlockType Type { get; }

        public event TesiraBlockInitializedEventHandler HasInitialized;

        private void OnDeviceCommunicatingChange(IConnectedItem device, bool communicating)
        {
            if (!communicating) return;
            ControlShouldInitialize();

            _subscribeTimer = new CTimer(specific =>
            {
                foreach (var value in _subscriptions)
                {
                    SendSubscribe(value.Key, value.Value);
                }
            }, 1000);
        }

        protected abstract void ControlShouldInitialize();

        private void OnReceivedData(TtpSshClient client, TesiraMessage message)
        {
            if (message.Type != TesiraMessageType.Notification && message.Id == InstanceTag)
            {
                if (!(message is TesiraResponse response) || response.Type != TesiraMessageType.OkWithResponse) return;
                Logger.Debug(GetType().Name + " \"" + InstanceTag + "\"", "Received {0}\r\n{1}",
                    response.AttributeCode, response.TryParseResponse().ToString(Formatting.Indented));
                ReceivedResponse(response);
            }
            else if (message.Type == TesiraMessageType.Notification && _subscriptions.ContainsKey(message.Id))
            {
                if (!(message is TesiraNotification response)) return;
                Logger.Debug(GetType().Name + " \"" + InstanceTag + "\"", "Received notification \"{0}\"\r\n{1}",
                    _subscriptions[message.Id], response.TryParseResponse().ToString(Formatting.Indented));
                ReceivedNotification(_subscriptions[message.Id], response.TryParseResponse());
                try
                {
                    AttributeChanged?.Invoke(this, _subscriptions[message.Id]);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        public event EventHandler<TesiraAttributeCode> AttributeChanged;

        protected abstract void ReceivedResponse(TesiraResponse response);
        protected abstract void ReceivedNotification(TesiraAttributeCode attributeCode, JToken data);

        public abstract void Subscribe();

        public virtual void Unsubscribe()
        {
            foreach (var publishToken in _subscriptions.Keys)
            {
                Unsubscribe(publishToken);
            }
        }

        protected void Subscribe(TesiraAttributeCode attributeCode)
        {
            var token = InstanceTag + "_" + attributeCode.ToCommandString();
            _subscriptions[token] = attributeCode;
            if (!Device.DeviceCommunicating) return;
            SendSubscribe(token, attributeCode);
        }

        private void SendSubscribe(string publishToken, TesiraAttributeCode attributeCode)
        {
            var message = Tesira.FormatBaseMessage(InstanceTag, TesiraCommand.Subscribe, attributeCode) + " \"" +
                          publishToken + "\" " + 200;
            Device.Send(message);
        }

        private void Unsubscribe(string publishToken)
        {
            if (_subscriptions.ContainsKey(publishToken))
            {
                if (Device.DeviceCommunicating)
                {
                    var message =
                        Tesira.FormatBaseMessage(InstanceTag, TesiraCommand.Unsubscribe, _subscriptions[publishToken]) +
                        " \"" + publishToken + "\"";
                    Device.Send(message);
                }

                _subscriptions.Remove(publishToken);
            }
        }

        protected virtual void OnInitialized()
        {
            Logger.Highlight("Tesira block {0} \"{1}\" OnInitialized()", GetType().Name, Name);
            var handler = HasInitialized;
            if (handler == null) return;
            try
            {
                handler(this);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }

    public enum TesiraBlockType
    {
        LevelControlBlock,
        MuteControlBlock,
        DialerBlock,
        LogicStateBlock,
        SourceSelectorBlock,
        InputBlock,
        DanteInputBlock,
        AecInputBlock
    }

    public delegate void TesiraBlockInitializedEventHandler(TesiraBlockBase block);
}