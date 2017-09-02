using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;

namespace Octagon.Formatik
{
    public abstract class DelimitedInput : Input
    {
        protected abstract string GetDelimiter();

        public override IEnumerable<IInputRecord> TryParse(Stream input, Encoding encoding, string recordsArrayPath, int limit = 0)
        {
            using (var reader = new StreamReader(input, encoding))
            {
                return TryParse(reader, limit).Records;
            }
        }

        public override (IEnumerable<IInputRecord> Records, string RecordsArrayPath) TryParse(string input, int limit = 0)
        {
            using (var reader = new StringReader(input))
            {
                return TryParse(reader, limit);
            }
        }

        private (IEnumerable<IInputRecord> Records, string RecordsArrayPath) TryParse(TextReader reader, int limit = 0)
        {
            try
            {
                using (var parser = new CsvParser(reader, new CsvConfiguration() { Delimiter = GetDelimiter(), ThrowOnBadData = true }))
                {
                    var records = new List<string[]>();
                    while (true)
                    {
                        var row = parser.Read();
                        if (row != null && (limit == 0 || records.Count < limit))
                            records.Add(row);
                        else
                            break;
                    }

                    return (
                        records
                            .Select((rec, i) => new DelimitedInputRecord(rec, i))
                            .ToArray(),
                        null
                    );
                }
            }
            catch (Exception e)
            {
                // eat any parsing exception and take it as a sign that this is not a delimited list of the expected type
            }

            return (null, null);
        }
    }
}