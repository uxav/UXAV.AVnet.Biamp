using System;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class AecInputChannel : IoChannelBase
    {
        internal AecInputChannel(TesiraBlockBase controlBlock, uint channelNumber)
            : base(controlBlock, channelNumber)
        {
            controlBlock.Device.Send(controlBlock.InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Gain,
                new[] { channelNumber });
        }

        public override bool SupportsVolumeLevel => true;

        public override bool SupportsMute => false;

        public override double Level
        {
            get => base.Level;
            set
            {
                if (!SupportsVolumeLevel)
                    throw new NotSupportedException("Control block is " + ControlBlock.Type);
                ControlBlock.Device.Send(ControlBlock.InstanceTag, TesiraCommand.Set, TesiraAttributeCode.Gain,
                    new[] { ChannelNumber }, value);
            }
        }

        public override double MinLevel => 0;

        public override double MaxLevel => 66;
    }
}