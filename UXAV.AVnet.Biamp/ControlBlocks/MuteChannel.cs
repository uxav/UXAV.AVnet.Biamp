namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class MuteChannel : IoChannelBase
    {
        internal MuteChannel(TesiraBlockBase controlBlock, uint channelNumber)
            : base(controlBlock, channelNumber)
        {
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Mute,
                new[] { channelNumber });
        }

        public override string Name { get; set; } = string.Empty;

        public override bool SupportsVolumeLevel => false;

        public override bool SupportsMute
        {
            get { return true; }
        }
    }
}