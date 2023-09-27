namespace UXAV.AVnet.Biamp
{
    public interface IChannels
    {
        IoChannelBase this[uint channel] { get; }
        int NumberOfChannels { get; }
    }
}