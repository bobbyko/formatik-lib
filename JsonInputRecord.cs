using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Octagon.Formatik
{
    public class JsonInputRecord: IInputRecord
    {
        private static readonly string[] emptyStringArray = new string[0];
        private static readonly Token[] emptyTokenArray = new Token[0];

        private JObject document;
        private IList<string> lines;

        public int Index { get; }

        public JsonInputRecord()
        {

        }

        public JsonInputRecord(JObject document, int index)
        {
            this.document = document;
            this.Index = index;
        }

        public JsonInputRecord(JObject document, int index, IList<string> lines) : this(document, index)
        {
            this.lines = lines;
        }

        private string GetDate(JToken jToken)
        {
            var lineInfo = (IJsonLineInfo)jToken;
            var line = lines[lineInfo.LineNumber - 1];  // lines are reported 1 based
            var startAt = line.LastIndexOf('"', lineInfo.LinePosition - 2) + 1;       // positions are reported 1 based
            return line.Substring(startAt, line.IndexOf('"', startAt) - startAt);
        }
        
        public string GetToken(string tokenSelector)
        {
            var jToken = document.SelectToken(tokenSelector);

            if (jToken != null)
            {
                switch (jToken.Type)
                {
                    case JTokenType.Date:
                        return jToken.Value<object>() != null ?
                            GetDate(jToken) :
                            string.Empty;

                    default:
                        return jToken.Value<string>();
                }
            }
            else
                return null;                
        }

        private int index;

        private IEnumerable<Token> GetTokens(JToken jToken)
        {
            switch (jToken.Type)
            {
                case JTokenType.Array:
                    return ((JArray)jToken)
                        .SelectMany(item => GetTokens(item)).ToArray();

                case JTokenType.Object:
                    return ((JObject)jToken).Properties()
                        .SelectMany(prop => GetTokens(prop.Value)).ToArray();

                case JTokenType.Date:
                    return jToken.Value<object>() != null ?
                        new Token[] { new Token(
                            this, 
                            GetDate(jToken), 
                            Regex.Replace(jToken.Path, "^\\[[0-9]+\\]\\.", ""), index++) } :
                        emptyTokenArray;

                default:
                    return string.IsNullOrEmpty(jToken.Value<string>()) ?
                        emptyTokenArray :
                        new Token[] { new Token(
                            this, 
                            jToken.Value<string>(), Regex.Replace(jToken.Path, "^\\[[0-9]+\\]\\.", ""), index++) };
            }
        }
        
        public IEnumerable<Token> GetTokens()
        {
            index = 0;
            return GetTokens(document);
        }
    }
}