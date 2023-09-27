 
namespace UXAV.AVnet.Biamp
{
    public class TesiraErrorResponse : TesiraResponse
    {
        internal TesiraErrorResponse(string command, string response)
            : base(command, response)
        {

        }

        public override TesiraMessageType Type
        {
            get { return TesiraMessageType.ErrorResponse; }
        }
    }
}