using System;
using Newtonsoft.Json.Linq;
using UXAV.AVnet.Core;
using UXAV.AVnet.Core.DeviceSupport;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public abstract class IoChannelBase : TesiraChannelBase, IVolumeControl
    {
        private double _level;
        private bool _mute;

        protected IoChannelBase(TesiraBlockBase controlBlock, uint channelNumber)
            : base(controlBlock, channelNumber)
        {
        }

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
                    case TesiraAttributeCode.MinLevel:
                        MinLevel = response.TryParseResponse()["value"].Value<double>();
                        break;
                    case TesiraAttributeCode.MaxLevel:
                        MaxLevel = response.TryParseResponse()["value"].Value<double>();
                        break;
                    case TesiraAttributeCode.Mute:
                        _mute = response.TryParseResponse()["value"].Value<bool>();
                        break;
                    case TesiraAttributeCode.Level:
                        _level = response.TryParseResponse()["value"].Value<double>();
                        VolumeLevelString = _level.ToString("F1") + " dB";
                        break;
                    case TesiraAttributeCode.Gain:
                        _level = response.TryParseResponse()["value"].Value<double>();
                        VolumeLevelString = _level.ToString("F1") + " dB";
                        break;
                }
            }
        }

        internal override void UpdateValue(TesiraAttributeCode attributeCode, JToken value)
        {
            switch (attributeCode)
            {
                case TesiraAttributeCode.Gain:
                    var gain = value.ToObject<double>();
#if DEBUG
                    Logger.Debug(Name + " new level value = " + _level.ToString("F1"));
#endif
                    if (Math.Abs(_level - gain) >= 0.1)
                    {
                        _level = gain;
                        VolumeLevelString = _level.ToString("F1") + " dB";
                        OnVolumeLevelChange(VolumeLevel);
                    }
                    break;
                case TesiraAttributeCode.Level:
                    var level = value.ToObject<double>();
#if DEBUG
                    Logger.Debug(Name + " new level value = " + _level.ToString("F1"));
#endif
                    if (Math.Abs(_level - level) >= 0.1)
                    {
                        _level = level;
                        VolumeLevelString = _level.ToString("F1") + " dB";
                        OnVolumeLevelChange(VolumeLevel);
                    }
                    break;
                case TesiraAttributeCode.Mute:
                    var mute = value.ToObject<bool>();
#if DEBUG
                    Logger.Debug(Name + " " + (mute ? "Muted" : "Unmuted"));
#endif
                    if (mute != _mute)
                    {
                        _mute = mute;
                        OnMuteChange(_mute);
                    }
                    break;
            }
        }

        public uint Id { get; }

        public override string Name { get; set; } = string.Empty;

        public virtual double Level
        {
            get => _level;
            set
            {
                if (!SupportsVolumeLevel)
                    throw new NotSupportedException("Control block is " + ControlBlock.Type);
                ControlBlock.Device.Send(ControlBlock.InstanceTag, TesiraCommand.Set, TesiraAttributeCode.Level,
                    new[] { ChannelNumber }, value);
            }
        }

        public string VolumeLevelString { get; private set; }
        public abstract bool SupportsMute { get; }

        public bool Muted
        {
            get => _mute;
            set
            {
                ControlBlock.Device.Send(ControlBlock.InstanceTag, TesiraCommand.Set, TesiraAttributeCode.Mute,
                    new[] {ChannelNumber}, value);
            }
        }

        public event MuteChangeEventHandler MuteChange;

        public event VolumeLevelChangeEventHandler VolumeLevelChange;

        public void SetDefaultVolumeLevel()
        {
            VolumeLevel = 50;
        }

        public void Mute()
        {
            Muted = true;
        }

        public void Unmute()
        {
            Muted = false;
        }

        public abstract bool SupportsVolumeLevel { get; }

        public ushort VolumeLevel
        {
            get => (ushort) Tools.ScaleRange(Level, MinLevel, MaxLevel, 0, 100);
            set => Level = Tools.ScaleRange(value, 0, 100, MinLevel, MaxLevel);
        }

        public virtual double MinLevel { get; private set; }

        public virtual double MaxLevel { get; private set; }

        protected void OnVolumeLevelChange(ushort level)
        {
            try
            {
                VolumeLevelChange?.Invoke(level);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        protected void OnMuteChange(bool muted)
        {
            try
            {
                MuteChange?.Invoke(muted);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}