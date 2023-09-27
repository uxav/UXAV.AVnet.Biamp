using Newtonsoft.Json.Linq;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class LevelChannel : IoChannelBase
    {
        private string _label;
        private string _name;

        internal LevelChannel(TesiraBlockBase controlBlock, uint channelNumber)
            : base(controlBlock, channelNumber)
        {
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Mute,
                new[] { channelNumber });
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.MinLevel,
                new[] { channelNumber });
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.MaxLevel,
                new[] { channelNumber });
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Level,
                new[] { channelNumber });
            _label = $"{controlBlock.InstanceTag} Level {channelNumber}";
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Label,
                new[] { channelNumber });
        }

        internal override void UpdateFromResponse(TesiraResponse response)
        {
            base.UpdateFromResponse(response);

            if (response.CommandType != TesiraCommand.Get) return;
            switch (response.AttributeCode)
            {
                    
                case TesiraAttributeCode.Label:
                    _label = response.TryParseResponse()["value"].Value<string>();
                    break;
            }
        }

        public override string Name
        {
            get => string.IsNullOrEmpty(_name) ? Label : _name;
            set => _name = value;
        }

        public string Label => _label;

        public override bool SupportsVolumeLevel => true;

        public override bool SupportsMute => true;
    }
}