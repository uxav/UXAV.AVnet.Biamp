using System;
using Newtonsoft.Json.Linq;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public abstract class TesiraMessage
    {
        private readonly string _message;
        private string _messageWithFix;

        protected TesiraMessage(string message)
        {
            _message = message;
        }

        public virtual string Message => _message;

        public JToken TryParseResponse()
        {
            try
            {
                if (string.IsNullOrEmpty(_messageWithFix))
                {
                    _messageWithFix = Tesira.FixJsonData(_message);
                }
                return JToken.Parse(_messageWithFix);
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return null;
            }
        }

        public abstract string Id { get; }

        public abstract TesiraMessageType Type { get; }
    }

    public enum TesiraMessageType
    {
        Ok,
        OkWithResponse,
        Notification,
        ErrorResponse
    }
}