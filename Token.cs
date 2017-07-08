using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Octagon.Formatik
{
    public class Token
    {
        public string InputSelector { get; set; }
        public int InputIndex { get; set; }
        public string OutputSelector { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }

        [IgnoreDataMember]
        public IEnumerable<TokenValue> Values { get; }

        public IEnumerable<string> DistinctValues
        {
            get
            {
                return Values != null ? 
                    Values.Select(value => value.Value).Distinct().OrderBy(value => value).ToArray() :
                    null;
            }
        }

        public Token() { }

        public Token(IInputRecord sampleRecord, string sampleValue, string inputSelector, int inputIndex)
        {
            if (sampleRecord == null)
                throw new ArgumentNullException("sampleRecord");

            if (string.IsNullOrEmpty(sampleValue))
                throw new ArgumentException("sampleValue cannot be null or empty string");

            this.Values = new TokenValue[] {
                new TokenValue(sampleValue, sampleRecord, this)
            };

            this.InputSelector = inputSelector;
            this.InputIndex = inputIndex;
        }

        public Token(IEnumerable<TokenValue> sampleValues, string inputSelector, int inputIndex)
        {
            if (sampleValues == null)
                throw new ArgumentNullException("sampleValues");

            if (!sampleValues.Any())
                throw new ArgumentException("sampleValues cannot be empty enumeration");

            this.Values = sampleValues.Select(value => new TokenValue(value.Value, value.Record, this)).ToArray();
            this.InputSelector = inputSelector;
            this.InputIndex = inputIndex;
        }

        public string GetOutput(IInputRecord rec)
        {
            if (string.IsNullOrEmpty(this.InputSelector))
                throw new FormatikException("InputSelector not specified");

            return $"{Prefix}{rec.GetToken(this.InputSelector)}{Suffix}";
        }
    }
}