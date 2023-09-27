using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public abstract class MultiChannelBlockBase<T> : TesiraBlockBase, IChannels, IEnumerable<T> where T : IoChannelBase
    {
        private readonly Dictionary<uint, T> _channels = new Dictionary<uint, T>();

        protected MultiChannelBlockBase(Tesira device, string instanceTag)
            : base(device, instanceTag)
        {
        }

        public virtual IoChannelBase this[uint channel] => _channels[channel];

        public int NumberOfChannels => _channels.Count;

        protected override void ControlShouldInitialize()
        {
            Device.Send(InstanceTag, TesiraCommand.Get, TesiraAttributeCode.NumChannels);
        }

        protected override void ReceivedResponse(TesiraResponse response)
        {
            if (response.OtherCommandElements.Any())
            {
                try
                {
                    var channel = uint.Parse(response.OtherCommandElements.First());
#if DEBUG
                    Logger.Debug("Response for channel " + channel);
#endif
                    if (_channels.ContainsKey(channel))
                    {
                        _channels[channel].UpdateFromResponse(response);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }

            if (response.CommandType != TesiraCommand.Get || response.OtherCommandElements.Any()) return;

            var json = response.TryParseResponse();

            if (json == null)
            {
                Logger.Error("{0} could not parse {1} value from json message \"{2}\"", GetType().Name,
                    response.AttributeCode, response.Message);
                return;
            }

            try
            {
                switch (response.AttributeCode)
                {
                    case TesiraAttributeCode.NumChannels:
                        var numberOfChannels = json["value"].Value<uint>();
                        for (uint i = 1; i <= numberOfChannels; i++)
                        {
                            if (!_channels.ContainsKey(i))
                            {
                                _channels[i] = CreateChannel(i);
                            }
                        }
                        OnInitialized();
                        break;
                    default:
                        UpdateAttribute(response.AttributeCode, json);
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Error("{0} could not parse {1} value from json \"{2}\", {3}", GetType().Name,
                    response.AttributeCode, json.ToString(), e.Message);
            }
        }

        protected abstract void UpdateAttribute(TesiraAttributeCode code, JToken data);

        protected abstract T CreateChannel(uint index);

        protected override void ReceivedNotification(TesiraAttributeCode attributeCode, JToken data)
        {
            switch (attributeCode)
            {
                case TesiraAttributeCode.Levels:
                {
                    var values = data["value"] as JArray;
                    uint channel = 0;
                    foreach (var value in values)
                    {
                        channel ++;
                        _channels[channel].UpdateValue(TesiraAttributeCode.Level, value);
                    }
                }
                    break;
                case TesiraAttributeCode.Mutes:
                {
                    var values = data["value"] as JArray;
                    uint channel = 0;
                    foreach (var value in values)
                    {
                        channel++;
                        _channels[channel].UpdateValue(TesiraAttributeCode.Mute, value);
                    }
                }
                    break;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _channels.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}