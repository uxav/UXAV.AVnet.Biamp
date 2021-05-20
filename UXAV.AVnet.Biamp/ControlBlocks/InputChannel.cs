namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class InputChannel : IoChannelBase
    {
        internal InputChannel(TesiraBlockBase controlBlock, uint channelNumber)
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
        }

        public override bool SupportsVolumeLevel => true;

        public override bool SupportsMute => true;
    }
}