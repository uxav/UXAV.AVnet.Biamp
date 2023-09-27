using Newtonsoft.Json.Linq;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class LogicStateChannel : TesiraChannelBase
    {
        private bool _state;

        internal LogicStateChannel(TesiraBlockBase controlBlock, uint channelNumber) : base(controlBlock, channelNumber)
        {
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Label,
                new[] { channelNumber });
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.State,
                new[] { channelNumber });
        }

        public bool State
        {
            get => _state;
            set
            {
                _state = value;
                ControlBlock.Device.Send(ControlBlock.InstanceTag, TesiraCommand.Set, TesiraAttributeCode.State,
                    new[] { ChannelNumber }, _state);
            }
        }

        public string Label { get; private set; } = string.Empty;

        internal override void UpdateFromResponse(TesiraResponse response)
        {
#if DEBUG
            Logger.Debug(ControlBlock.InstanceTag + " Channel " + ChannelNumber,
                "Received {0} response for {1}: {2}", response.CommandType, response.AttributeCode,
                response.TryParseResponse().ToString());
#endif
            if (response.CommandType == TesiraCommand.Get)
            {
                switch (response.AttributeCode)
                {
                    case TesiraAttributeCode.Label:
                        Label = response.TryParseResponse()["value"].Value<string>();
                        break;
                    case TesiraAttributeCode.State:
                        _state = response.TryParseResponse()["value"].Value<bool>();
                        break;
                }
            }
        }

        internal override void UpdateValue(TesiraAttributeCode attributeCode, JToken value)
        {
            
        }
    }
}