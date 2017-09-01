using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Octagon.Formatik
{
    public class DelimitedInputRecord : IInputRecord
    {
        private static readonly string[] emptyStringArray = new string[0];
        private static readonly Token[] emptyTokenArray = new Token[0];

        private string[] record;


        public int Index { get; }


        public DelimitedInputRecord()
        {

        }

        public DelimitedInputRecord(string[] record, int index)
        {
            this.record = record;
            this.Index = index;
        }

        public string GetToken(string tokenSelector)
        {
            var selectorIndexMatch = Regex.Match(tokenSelector, "[(\\d)]");
            if (selectorIndexMatch.Success)
            {
                if (int.TryParse(selectorIndexMatch.Groups[0].Value, out var index))
                {
                    return record.Length > index ?
                        record[index] :
                        null;
                }
            }

            throw new FormatikException($"[{this.GetType().Name}] Unrecognized token selector '{tokenSelector}'");
        }

        public IEnumerable<Token> GetTokens()
        {
            return record.Select((rec, i) =>
                new Token(
                    this,
                    rec,
                    $"[{i}]",
                    i)
                )
                .ToArray();
        }
    }
}