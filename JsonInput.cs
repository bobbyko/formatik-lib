using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Octagon.Formatik
{
    public class JsonInput: Input
    {
        private static Input instance;

        public static Input Factory() {
            if (instance == null)
                instance = new JsonInput();

            return instance;
        }

        /// <summary>
        /// Finds the largest record array and assumes its the records array
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static void FindRecordArray(JToken obj, ref JArray largestArray)
        {
            if (obj.Type == JTokenType.Array &&
                ((JArray)obj).Count() >= 3 &&
                (largestArray == null || ((JArray)obj).Count() > largestArray.Count()))
            {
                largestArray = (JArray)obj;
            }
            else
                foreach (var prop in ((JObject)obj).Properties())
                    FindRecordArray(prop.Value, ref largestArray);
        }

        public override IEnumerable<IInputRecord> TryParse(Stream input, Encoding encoding, string recordsArrayPath, int limit = 0)
        {
            var serializer = new JsonSerializer();
            var lines = new List<string>();

            using (var sr = new StreamReader(input, encoding, false, 8192, true))
            {
                var line = sr.ReadLine();
                while (line != null && limit == 0 || lines.Count <= limit)
                {
                    lines.Add(line);
                    line = sr.ReadLine();
                }
            }

            input.Seek(0, SeekOrigin.Begin);

            using (var sr = new StreamReader(input, encoding))
            {
                using (var jsonTextReader = new JsonTextReader(sr))
                {
                    JArray records;

                    if (string.IsNullOrEmpty(recordsArrayPath))
                    {
                        records = JArray.Load(jsonTextReader, new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
                    }
                    else
                    {
                        var document = (JToken)JToken.Load(jsonTextReader, new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
                        records = string.IsNullOrEmpty(recordsArrayPath) ?
                            (JArray)document :
                            (JArray)((JObject)document).SelectToken(recordsArrayPath);
                    }

                    return records
                        .Take(limit > 0 ? limit : int.MaxValue)
                        .Select((jRecord, i) => new JsonInputRecord((JObject)jRecord, i, lines))
                        .ToArray();
                }
            }
        }


        public override (IEnumerable<IInputRecord> Records, string RecordsArrayPath) TryParse(string input, int limit = 0)
        {
            if (Regex.IsMatch(input, "^(/\\*([^*]|[\r\n]|(\\*+([^*/]|[\r\n])))*\\*+/)|(//.*)*[{\\[]", RegexOptions.IgnoreCase & RegexOptions.Multiline))
            {
                JArray records = null;
                string[] lines = null;

                Parallel.Invoke(
                    () =>
                    {
                        JToken json; 
                        try
                        {
                            json = JToken.Parse(input, new JsonLoadSettings() { CommentHandling = CommentHandling.Ignore });
                        }
                        catch (JsonReaderException)
                        {
                            json = null;
                        }

                        if (json != null)
                            FindRecordArray(json, ref records);
                    },
                    () =>
                    {
                        lines = input.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
                    }
                );

                if (records == null)
                    return (
                        null,
                        null
                    );
                else
                    return (
                        records
                            .Take(limit > 0 ? limit : int.MaxValue)
                            .Select((rec, i) => new JsonInputRecord((JObject)rec, i, lines))
                            .ToArray(),

                        records.Path
                    );
            }

            return (null, null);
        }
    }
}