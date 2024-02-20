using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UXAV.Logging;

namespace UXAV.AVnet.Biamp
{
    public class TesiraResponse : TesiraMessage
    {
        private string _command;
        private readonly List<string> _otherCommandElements = new List<string>();
        private string _instanceId;

        internal TesiraResponse(string command, string message)
            : base(message)
        {
            _instanceId = string.Empty;
            if (string.IsNullOrEmpty(command))
            {
                throw new Exception("command argument cannot be null or empty");
            }
            Command = command;
        }

        public string Command
        {
            get => _command;
            private set
            {
                _command = value;

                if(this is TesiraErrorResponse) return;

                try
                {
                    var matches = Regex.Matches(_command, @"\w+|([""'])(?:(?=(\\?))\2.)*?\1");
                    var words = (from Match match in matches
                        select match.Groups[1].Value == "\""
                            ? match.Value.Substring(1, match.Value.Length - 2)
                            : match.Value).ToList();

                    if (words.Count > 0)
                    {
                        _instanceId = words[0];
                    }

                    if (words.Count > 1)
                    {
                        try
                        {
                            CommandType = (TesiraCommand) Enum.Parse(typeof (TesiraCommand), words[1], true);
                        }
                        catch (ArgumentException e)
                        {
                            Logger.Error("Could not parse TesiraCommand from \"{0}\"", words[1]);
                            throw e;
                        }
                    }

                    if (words.Count > 2 && CommandType != TesiraCommand.RecallPresetByName)
                    {
                        try
                        {
                            AttributeCode = (TesiraAttributeCode)Enum.Parse(typeof(TesiraAttributeCode), words[2], true);
                        }
                        catch (ArgumentException e)
                        {
                            Logger.Error("Could not parse TesiraAttributeCode from \"{0}\"", words[2]);
                            throw e;
                        }

                        if (words.Count <= 3) return;

                        for (var i = 3; i < words.Count; i++)
                        {
                            _otherCommandElements.Add(words[i]);
                        }
                    }
                    else if (CommandType == TesiraCommand.RecallPresetByName)
                    {
                        if (words.Count <= 2) return;

                        for (var i = 2; i < words.Count; i++)
                        {
                            _otherCommandElements.Add(words[i]);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        public override string Id => _instanceId;

        public TesiraCommand CommandType { get; private set; }

        public TesiraAttributeCode AttributeCode { get; private set; }

        public string[] OtherCommandElements => _otherCommandElements.ToArray();

        public override TesiraMessageType Type => string.IsNullOrEmpty(Message) ? TesiraMessageType.Ok : TesiraMessageType.OkWithResponse;

        public override string ToString()
        {
            return Type == TesiraMessageType.Ok ? Command : $"{Command}: {Message}";
        }
    }
}