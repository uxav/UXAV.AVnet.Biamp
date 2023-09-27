using Newtonsoft.Json.Linq;

namespace UXAV.AVnet.Biamp.ControlBlocks
{
    public sealed class MuteControlBlock : MultiChannelBlockBase<MuteChannel>
    {
        internal MuteControlBlock(Tesira device, string instanceId)
            : base(device, instanceId)
        {
            if (device.DeviceCommunicating)
            {
                ControlShouldInitialize();
            }
        }

        public override TesiraBlockType Type => TesiraBlockType.MuteControlBlock;

        public bool Ganged { get; private set; }

        protected override void ControlShouldInitialize()
        {
            base.ControlShouldInitialize();
            Device.Send(InstanceTag, TesiraCommand.Get, TesiraAttributeCode.Ganged);
        }

        public override void Subscribe()
        {
            Subscribe(TesiraAttributeCode.Mutes);
        }

        protected override void UpdateAttribute(TesiraAttributeCode code, JToken data)
        {
            switch (code)
            {
                case TesiraAttributeCode.Ganged:
                    Ganged = data["value"].Value<bool>();
                    break;
            }
        }

        protected override MuteChannel CreateChannel(uint index)
        {
            return new MuteChannel(this, index);
        }
    }
}