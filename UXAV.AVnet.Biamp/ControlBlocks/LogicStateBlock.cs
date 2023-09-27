using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class LogicStateBlock : TesiraBlockBase
    {
        private readonly Dictionary<uint, LogicStateChannel> _channels = new Dictionary<uint, LogicStateChannel>();

        internal LogicStateBlock(Tesira device, string instanceTag)
            : base(device, instanceTag)
        {

        }

        public override TesiraBlockType Type => TesiraBlockType.LogicStateBlock;

        public LogicStateChannel this[uint channel] => _channels[channel];

        protected override void ControlShouldInitialize()
        {
            if (!_channels.ContainsKey(1))
            {
                _channels[1] = new LogicStateChannel(this, 1);
            }
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
            }
        }

        protected override void ReceivedNotification(TesiraAttributeCode attributeCode, JToken data)
        {
            
        }

        public override void Subscribe()
        {
            
        }
    }
}