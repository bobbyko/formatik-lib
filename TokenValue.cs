using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Octagon.Formatik
{
    public class TokenValue
    {
        [IgnoreDataMember]
        public Token Token { get; }
        public virtual string Value { get; }
        [IgnoreDataMember]
        public IInputRecord Record { get; }

        public TokenValue() { }
        protected TokenValue(IInputRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            this.Record = record;
        }

        public TokenValue(string value, IInputRecord record, Token token)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentNullException("value");

            if (record == null)
                throw new ArgumentNullException("record");

            this.Value = value;
            this.Record = record;
            this.Token = token;
        }

        public TokenValue(Token token)
        {
            if (token == null)
                throw new ArgumentNullException("token");

            if (!token.Values.Any())
                throw new ArgumentException("token does not contain any sample values");

            var firstSample = token.Values.First();
            this.Value = firstSample.Value;
            this.Record = firstSample.Record;
        }
    }
}