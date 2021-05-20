using Newtonsoft.Json.Linq;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public class AecInputBlock : MultiChannelBlockBase<AecInputChannel>
    {
        internal AecInputBlock(Tesira device, string instanceTag)
            : base(device, instanceTag)
        {
        }

        public override TesiraBlockType Type => TesiraBlockType.AecInputBlock;

        protected override void ControlShouldInitialize()
        {
            Device.Send(InstanceTag, TesiraCommand.Get, TesiraAttributeCode.NumChannels);
        }

        protected override void UpdateAttribute(TesiraAttributeCode code, JToken data)
        {

        }

        public override void Subscribe()
        {
            Subscribe(TesiraAttributeCode.Levels);
        }

        protected override AecInputChannel CreateChannel(uint index)
        {
            return new AecInputChannel(this, index);
        }
    }
}