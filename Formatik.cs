using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Octagon.Formatik
{
    public class Formatik : IComparable, IComparable<Formatik>, IEquatable<Formatik>
    {
        private static Dictionary<char, decimal> markupCharWeights = new Dictionary<char, decimal>() {
            { '\r', 2m },
            { '\n', 2m },
            { '|', 2m },
            { ';', 2m },
            { '~', 2m },
            { '\t', 1m },
            { ',', 1m },
            { '\'', 1m },
            { '"', 1m },
            { '{', 1m },
            { '}', 1m },
            { '[', 1m },
            { ']', 1m },
            { '(', 1m },
            { ')', 1m },
            { '<', 1m },
            { '>', 1m },
            { '=', 1m },
            { ':', 1m },
            { '+', 1m },
            { '_', 1m },
            { '-', 1m },
            { '&', 1m },
            { '?', 1m },
            { '\\', 1m },
            { '/', 1m },
            { '*', 1m },
            { '#', 1m },
            { '@', 1m },
            { '!', 1m }
        };

        private static char[] selectorsSplitChars = new char[] {
            '.',
            ' ',
            ']'
        };

        private readonly string[] EmptyStringArray = new string[0];
        private readonly int[] EmptyIntArray = new int[0];

        private class EnumerableComparer<T> : IComparer<IEnumerable<T>>
        {
            private string type = typeof(T).Name;

            public int Compare(IEnumerable<T> aa, IEnumerable<T> bb)
            {
                if (!aa.Any() && !bb.Any())
                    return 0;

                if (!aa.Any() && bb.Any())
                    return -1;

                if (aa.Any() && !bb.Any())
                    return 1;

                var a = aa.First();
                var b = bb.First();

                var result = Comparer.Default.Compare(a, b);

                return result == 0 ?
                    this.Compare(aa.Skip(1), bb.Skip(1)) :
                    result;
            }
        }

        private static string version = typeof(Formatik)
            .GetTypeInfo()
            .Assembly
            .CustomAttributes
            .First(attr => attr.AttributeType == typeof(AssemblyInformationalVersionAttribute))
            .ConstructorArguments
            .First()
            .Value
            .ToString();

        public string Version
        {
            get
            {
                return version;
            }

            set
            {
                if (value != version)
                    throw new FormatikException("Invalid version. Are you trying to load a Format created from a Formatik version?");
            }
        }

        public int MaxDegreeOfParallelism { get; set; } = 2;
        public int MaxInputRecords { get; set; } = 1000;

        public string Header { get; set; }
        public string Footer { get; set; }
        public IEnumerable<string> Separators { get; set; }

        public string Input { get; set; }

        public int InputHash { get; set; }

        public string Example { get; set; }

        public int ExampleHash { get; set; }
        public string ExamplePlaceholder { get; }
        public int Cardinality { get; set; }

        public InputFormat InputFormat { get; set; }
        public string RecordsArrayPath { get; set; }

        public IEnumerable<Token> Tokens { get; set; }

        public int Hash
        {
            get
            {
                return GetHashCode();
            }

            set
            {
                // if (value != GetHashCode())
                //     throw new FormatikException("Invalid Hash");
            }
        }

        public Formatik()
        {
            Cardinality = 3;
        }

        public Formatik(string input, string example) : this()
        {
            Input = input;
            InputHash = GetRepeatableHashCode(input);
            Example = example;
            ExampleHash = GetRepeatableHashCode(example);
            ExamplePlaceholder = GetUnusedCharacter(example);

            Evaluate();
        }

        protected Formatik(string header, string footer, string input, string example, string examplePlaceholder, int cardinality)
        {
            Header = header;
            Footer = footer;
            Input = input;
            InputHash = GetRepeatableHashCode(input);
            Example = example;
            ExampleHash = GetRepeatableHashCode(example);
            ExamplePlaceholder = examplePlaceholder;
            Cardinality = cardinality;
        }

        /// <summary>
        /// Returns an unused character that can be used as a placeholder
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static string GetUnusedCharacter(string text, IEnumerable<string> otherPlaceholders = null)
        {
            string placeholder = null;
            for (var code = 33; code < 126; code++)
                if (code != 34 &&       // Dont like the " as placeholder simply because confuses for human readability
                    text.IndexOf((char)code) < 0 &&
                    (otherPlaceholders == null || !otherPlaceholders.Contains(((char)code).ToString())))
                {
                    placeholder = ((char)code).ToString();
                    break;
                }

            if (placeholder == null)
                throw new FormatikException("Could not find suitable token charecter placeholder");

            return placeholder;
        }

        private IEnumerable<string> GetOutputSeparators(IEnumerable<Token> tokens)
        {
            var markup = Example;
            foreach (var tokenVal in tokens.SelectMany(token => token.Values.Select(sample => sample.Value)))
                markup = markup.Replace(tokenVal, ExamplePlaceholder);

            // find the first order separators - aka row separators
            var _1stOrderSeparators = new List<string>();
            var tokenizedInput = tokens
                .SelectMany(token => token.Values)
                .GroupBy(value => value.Record)
                .ToArray();

            var l = markup.Length;

            for (var sl = 1; sl < l - 1; sl++)
            {
                foreach (var separator in markup.ToCharArray()
                    .Where((c, i) => i < l - sl)
                    .Select((c, i) => new String(markup.Skip(i).Take((int)sl).ToArray()))
                    .Where(sep => !sep.Contains(ExamplePlaceholder))
                    .GroupBy(sep => sep)
                    .Where(g => !g.Key.Contains(ExamplePlaceholder))
                    .Select(g => g.Key)
                    .Where(sep => Example
                        .Split(new string[] { sep }, StringSplitOptions.None)
                        .Count(potentialRec => tokenizedInput
                            .Any(tokenizedRecord => tokenizedRecord
                                .All(token => potentialRec.Contains(token.Value))
                        )) == Cardinality))
                {
                    _1stOrderSeparators.Add(separator);
                }
            };

            // run a validation loop by splitting the example using the potential separators, until we pass the requirement
            // that all rows have the same number of values

            var excluded1stOrderSeparators = new List<string>();
            IList<string> separators;

            do
            {
                var _1stOrderSeparator =
                    _1stOrderSeparators
                        .Except(excluded1stOrderSeparators)
                        .OrderByDescending(sep => sep.Length)
                        .ThenByDescending(sep => sep.ToCharArray().Sum(c =>
                        {
                            return markupCharWeights.TryGetValue(c, out var weight) ?
                                weight :
                                0;
                        }))
                        .FirstOrDefault();

                if (_1stOrderSeparator == null)
                    throw new FormatikException("Could not find 1st Order separator in example");

                separators = new List<string>() { _1stOrderSeparator };

                var splitMarkup = markup.Split(new string[] { _1stOrderSeparator }, StringSplitOptions.None);
                var splitExample = Example.Split(new string[] { _1stOrderSeparator }, StringSplitOptions.None);
                var records = splitExample
                    .Select((rec, i) => new { Record = rec, SplitMarkup = splitMarkup[i] })
                    .ToArray();

                if (splitMarkup.Any(recMarkup => recMarkup != ExamplePlaceholder))
                {
                    // find 2nd order separators
                    l = markup.Length;
                    var _2stOrderSeparators = new List<string>();
                    var recordTokenCardinality = tokenizedInput
                        .GroupBy(group => group.Count())
                        .Where(distGroup => distGroup.Count() >= Cardinality)
                        .Max(distGroup => distGroup.Key);

                    var excluded2ndOrderSeparators = new List<string>();

                    for (var sl = 1; sl < l - 1; sl++)
                    {
                        foreach (var record in records)
                        {
                            foreach (var separator in record.SplitMarkup.ToCharArray()
                                .Where((c, i) => i < l - sl)
                                .Select((c, i) => new String(markup.Skip(i).Take((int)sl).ToArray()))
                                .Where(sep => !sep.Contains(ExamplePlaceholder))
                                .GroupBy(sep => sep)
                                .Select(g => g.Key)
                                .Where(sep => record.Record
                                    .Split(new string[] { sep }, StringSplitOptions.None)
                                    .Count(potentialToken => tokenizedInput
                                        .Any(tokenRow => tokenRow
                                            .Any(token => MismatchFactor(potentialToken, token.Value) < 1)
                                    )) == recordTokenCardinality))
                            {
                                _2stOrderSeparators.Add(separator);
                            }
                        }
                    };

                    do
                    {
                        var _2stOrderSeparator =
                            _2stOrderSeparators
                                .Except(excluded2ndOrderSeparators)
                                .Distinct()
                                .OrderByDescending(sep => sep.Length)
                                .ThenByDescending(sep => sep.ToCharArray().Sum(c =>
                                {
                                    return markupCharWeights.TryGetValue(c, out var weight) ?
                                        weight :
                                        0;
                                }))
                                .FirstOrDefault();

                        if (_2stOrderSeparator == null)
                        {
                            excluded1stOrderSeparators.Add(_1stOrderSeparator);
                            break;  // ran out of 2nd order separators to try, exit inner loop and try another 1st order separator
                        }

                        separators.Add(_2stOrderSeparator);

                        if (!ValidateSeparators(tokens, separators))
                        {
                            excluded2ndOrderSeparators.Add(_2stOrderSeparator);
                            separators.RemoveAt(1);
                        }
                        else
                            return separators;
                    }
                    while (true);
                }
                else
                {
                    if (!ValidateSeparators(tokens, separators))
                        excluded1stOrderSeparators.Add(_1stOrderSeparator);
                    else
                        return separators;
                }
            }
            while (true);
        }

        private static int Occurances(string text, string value, int startAt = 0)
        {
            var indexOf = text.IndexOf(value, startAt);
            if (indexOf >= 0)
                return 1 + Occurances(text, value, startAt + value.Length);
            else
                return 0;
        }

        private static string ReplaceOne(string text, string value, string replaceValue)
        {
            var index = text.IndexOf(value);
            if (index >= 0)
                return $"{text.Substring(0, index)}{replaceValue}{text.Substring(index + value.Length)}";
            else
                return text;
        }

        private static string ReplaceOne(string text, IEnumerable<string> values, string replaceValue)
        {
            var resedue = text;
            foreach (var value in values)
                resedue = ReplaceOne(resedue, value, replaceValue);

            return resedue;
        }

        private void ResedueContains(string resedue, string[] values, int startAt, string[] valueSet, IList<string[]> valueSets)
        {
            var l = values.Length;
            for (var i = startAt; i < l; i++)
            {
                var nextValue = values[i];

                if (resedue.Contains(nextValue))
                {
                    var nextValueSet = valueSet.Append(nextValue).ToArray();
                    if (valueSet.Length < Cardinality - 1)
                        ResedueContains(
                            ReplaceOne(resedue, nextValue, ExamplePlaceholder),
                            values,
                            i + 1,
                            nextValueSet,
                            valueSets);
                    else
                    {
                        if (valueSets.All(set => set
                                .Take(nextValueSet.Length)
                                .TakeWhile((val, index) => val == nextValueSet[index])
                                .Count() < set.Length))
                        {
                            valueSets.Add(nextValueSet);
                        }
                    }
                }
            }
        }

        private void EvaluateSubset(IEnumerable<Token> allTokens, Token[] set, Token nextToken, string resedue, int[] sortedRecordSubset,
            ConcurrentBag<Token[]> result)
        {
            // check that the join of the current recordSet and the new Token's values records can still meet the minimum cardinality requirement
            if (nextToken.Values
                    .Where(value => Array.BinarySearch<int>(sortedRecordSubset, value.Record.Index) > -1)
                    .Take(Cardinality + 1)
                    .Count() < Cardinality)
            {
                return;
            }

            var newSortedRecordSubset = sortedRecordSubset
                .Intersect(nextToken.Values
                    .Select(value => value.Record.Index))
                .OrderBy(index => index)
                .ToArray();

            var valueSets = new List<string[]>();

            ResedueContains(
                resedue,
                nextToken.Values
                    .Where(value => Array.BinarySearch<int>(newSortedRecordSubset, value.Record.Index) > -1)
                    .Select(value => value.Value)
                    .GroupBy(value => value)
                    .SelectMany(group => group.Take(Cardinality))
                    .OrderByDescending(value => value.Length)
                    .ThenBy(value => value)
                    .ToArray(),
                0,
                new string[0],
                valueSets);

            if (valueSets.Count > 0)
            {
                var nextSet = set.Append(nextToken).ToArray();
                result.Add(nextSet);

                IDictionary<string, dynamic> deadends = null;

                foreach (var valueSet in valueSets)
                {
                    var nextResedue = ReplaceOne(resedue, valueSet, ExamplePlaceholder);
                    var nextSortedRecordSubset = sortedRecordSubset
                        .Intersect(nextToken.Values
                            .Where(value => valueSet.Contains(value.Value))
                            .Select(value => value.Record.Index)
                            .ToArray())
                        .ToArray();

                    string valueSetKey = null;
                    if (deadends != null)
                    {
                        valueSetKey = string.Join("", valueSet.Distinct());

                        if (deadends.TryGetValue(valueSetKey, out var deadend) &&
                            deadend.Resedue == nextResedue &&
                            deadend.RecordSubset.Intersect(nextSortedRecordSubset).Count() == nextSortedRecordSubset.Length)
                        {
                            continue;
                        }
                    }

                    if (nextSortedRecordSubset.Any())
                    {
                        var lastCount = result.Count;

                        SetDiscovery(allTokens, nextSet, nextResedue, nextSortedRecordSubset, result);

                        if (result.Count == lastCount && valueSets.Count > 1)
                        {
                            if (deadends == null)
                            {
                                deadends = new Dictionary<string, dynamic>();
                                valueSetKey = string.Join("", valueSet.Distinct());
                            }

                            deadends.Add(valueSetKey, new { Resedue = nextResedue, RecordSubset = nextSortedRecordSubset });
                        }
                    }
                }
            }
        }

        private void SetDiscovery(IEnumerable<Token> allTokens, Token[] set, string resedue, int[] sortedRecordSubset,
            ConcurrentBag<Token[]> result)
        {
            var lastToken = set.LastOrDefault();

            if (lastToken == null)
            {
                foreach (var nextToken in allTokens)
                    EvaluateSubset(allTokens, set, nextToken, resedue, sortedRecordSubset, result);
            }
            else
            {
                foreach (var nextToken in allTokens.SkipWhile(token => token != lastToken).Skip(1))
                    EvaluateSubset(allTokens, set, nextToken, resedue, sortedRecordSubset, result);
            }
        }

        private IEnumerable<Token> GetTokens(IEnumerable<IInputRecord> records)
        {
            var allTokens = records
                .Select(rec => rec
                    .GetTokens()
                    .Where(token => token.Values.Any(sample => Example.Contains(sample.Value)))
                    .ToArray()
                )
                .SelectMany(rec => rec)
                .GroupBy(token => token.InputSelector)
                .Where(group => group.Count() >= Cardinality)
                .OrderBy(group => group.Select((token, i) => i).Sum())
                .Select(group => new Token(group.SelectMany(token => token.Values).ToArray(), group.Key, group.First().InputIndex))
                .ToArray();

            var validTokenSets = new ConcurrentBag<Token[]>();

            var setDiscoveryTimer = new Stopwatch();
            setDiscoveryTimer.Start();
            Debug.Write($"Set discovery of {allTokens.Count()} tokens...");

            SetDiscovery(
                allTokens
                    .OrderByDescending(token =>
                        token.Values
                            .Select(value => value.Value)
                            .Distinct()
                            .Count() *
                        token.Values
                            .Select(value => value.Value)
                            .OrderByDescending(value => value.Length)
                            .Take(Cardinality)
                            .Sum(value => value.Length))
                    .ToArray(),
                new Token[0],
                Example,
                allTokens
                    .SelectMany(token => token.Values.Select(value => value.Record.Index))
                    .Distinct()
                    .OrderBy(index => index)
                    .ToArray(),
                validTokenSets);

            setDiscoveryTimer.Stop();
            Debug.WriteLine($"done in {(int)setDiscoveryTimer.Elapsed.TotalMilliseconds}ms");

            return validTokenSets
                .OrderBy(set => ReplaceOne(Example, set.SelectMany(token => token.Values.Select(val => val.Value).Take(Cardinality)), string.Empty).Length)
                .ThenBy(set => set.Sum(token => token.InputSelector.Split(selectorsSplitChars).Count()))
                .ThenBy(set => set.Select(token => token.InputSelector).OrderBy(inputSelector => inputSelector).ToArray(), new EnumerableComparer<string>())
                .FirstOrDefault();
        }

        private string[][] GetTable(IEnumerable<string> separators)
        {
            var secondSeparator = separators.Skip(1).FirstOrDefault();

            return Example
                .Split(new string[] { separators.First() }, StringSplitOptions.None)
                .Select(row => string.IsNullOrEmpty(secondSeparator) ?
                    new string[] { row } :
                    row.Split(new string[] { secondSeparator }, StringSplitOptions.None))
                .ToArray();
        }

        private Boolean ValidateSeparators(IEnumerable<Token> tokens, IEnumerable<string> separators)
        {
            var table = GetTable(separators);
            var tokenValuesByRecord = tokens
                .SelectMany(token => token.Values)
                .GroupBy(value => value.Record.Index)
                .ToArray();

            return table
                .Where(row => tokenValuesByRecord
                    .Any(tokenValueRow => tokenValueRow
                        .All(value => row
                            .Any(cell => MismatchFactor(cell, value.Value) < 1))))
                .Count() == Cardinality;
        }

        private static float MismatchFactor(string cellValue, string rawValue)
        {
            return (float)ReplaceOne(cellValue, rawValue, string.Empty).Length / cellValue.Length;
        }

        private IEnumerable<Token> PurifyTokens(IEnumerable<Token> rawTokens, IEnumerable<string> separators)
        {
            var table = GetTable(separators);

            var tokens = rawTokens
                .Where(token => token.Values
                    .Any(value => table
                        .Any(row => row
                            .Any(cell => MismatchFactor(cell, value.Value) < 1))))
                .ToArray();

            foreach (var token in tokens)
            {
                token.OutputSelector =
                    token.Values
                        .Select(value => table
                            .Select(row => row
                                .Select((cellValue, i) => new { Index = i, MismatchFactor = MismatchFactor(cellValue, value.Value) })
                                .Where(cellMeta => cellMeta.MismatchFactor < 1)
                                .OrderBy(cellMeta => cellMeta.MismatchFactor)
                                .FirstOrDefault())
                            .Where(cellMeta => cellMeta != null)
                            .Select(cellMeta => cellMeta.Index)
                            .GroupBy(index => index)
                            .OrderByDescending(group => group.Count())
                            .First().First())
                        .GroupBy(index => index)
                        .OrderByDescending(group => group.Count())
                        .First().First()
                        .ToString();
            }

            return tokens
                .OrderBy(token => int.Parse(token.OutputSelector))
                .ToArray();
        }

        private void SetTokenWrappers(IEnumerable<Token> tokens, IEnumerable<string> separators)
        {
            var normalizedInputRecordIndexes = tokens
                .SelectMany(token => token.Values.Select(value => value.Record.Index).ToArray())
                .Distinct()
                .OrderBy(recordIndex => recordIndex)
                .TakeWhile((recordIndex, i) => i < Cardinality)
                .ToArray();

            var firstRecordValues = tokens
                .SelectMany(token => token.Values)
                .Where(value => value.Record.Index == 0)
                .ToArray();

            // this will allow us to skip potential "header" rows
            var table = GetTable(separators)
                .SkipWhile(row => !firstRecordValues
                    .All(tokenValue => row.Any(cell => MismatchFactor(cell, tokenValue.Value) < 1)))
                .Take(Cardinality)
                .ToArray();

            foreach (var token in tokens)
            {
                var wrappers = token.Values
                    .Where(tokenValue => Array.IndexOf<int>(normalizedInputRecordIndexes, tokenValue.Record.Index) > -1)
                    .Select(tokenValue =>
                    {
                        var cellValue = table[Array.IndexOf<int>(normalizedInputRecordIndexes, tokenValue.Record.Index)]
                            .First(cell => MismatchFactor(cell, tokenValue.Value) < 1);

                        var tokenStartsAt = cellValue.IndexOf(tokenValue.Value);

                        var tokenEndsAt = cellValue.IndexOf(tokenValue.Value);
                        if (tokenEndsAt >= 0)
                            tokenEndsAt += tokenValue.Value.Length;

                        return new
                        {
                            Prefix = tokenStartsAt > 0 ? cellValue.Substring(0, tokenStartsAt) : "",
                            Suffix = tokenEndsAt >= 0 && tokenEndsAt < cellValue.Length ? cellValue.Substring(tokenEndsAt) : ""
                        };
                    })
                    .ToArray();

                token.Prefix = wrappers
                    .GroupBy(wrapper => wrapper.Prefix)
                    .OrderByDescending(group => group.Count())
                    .First().Key;

                token.Suffix = wrappers
                    .GroupBy(wrapper => wrapper.Suffix)
                    .OrderByDescending(group => group.Count())
                    .First().Key;
            }
        }

        private string GetFooter(IEnumerable<string> tokens, IEnumerable<string> separators)
        {
            return string.Join(separators.First(), Example
                .Split(new String[] { separators.First() }, StringSplitOptions.None)
                .Reverse()
                .TakeWhile(row => !tokens.Any(token => row.Contains(token))));
        }

        private IEnumerable<IInputRecord> GetRecords(int limit = 0)
        {
            var inputParse = JsonInput.Factory().TryParse(Input, limit);
            if (inputParse.Records != null)
            {
                InputFormat = InputFormat.JSON;
                RecordsArrayPath = inputParse.RecordsArrayPath;
                return inputParse.Records;
            }

            inputParse = XmlInput.Factory().TryParse(Input, limit);
            if (inputParse.Records != null)
            {
                InputFormat = InputFormat.XML;
                RecordsArrayPath = inputParse.RecordsArrayPath;
                return inputParse.Records;
            }

            inputParse = CsvInput.Factory().TryParse(Input, limit);
            if (inputParse.Records != null)
            {
                InputFormat = InputFormat.CSV;
                RecordsArrayPath = inputParse.RecordsArrayPath;
                return inputParse.Records;
            }

            inputParse = TsvInput.Factory().TryParse(Input, limit);
            if (inputParse.Records != null)
            {
                InputFormat = InputFormat.TSV;
                RecordsArrayPath = inputParse.RecordsArrayPath;
                return inputParse.Records;
            }

            throw new FormatikException("Unable to detect input format.");
        }

        protected void Evaluate()
        {
            var inputRecords = GetRecords(MaxInputRecords);

            var rawTokens = GetTokens(inputRecords);

            if (rawTokens == null || !rawTokens.Any())
                throw new FormatikException("Could not find any similarities between the input document and the example");

            this.Separators = GetOutputSeparators(rawTokens);

            Tokens = PurifyTokens(rawTokens, Separators);

            // construct output template 
            SetTokenWrappers(Tokens, Separators);

            var firstToken = Tokens.First();
            var lastToken = Tokens.Last();

            var headerSize = firstToken.Values
                .Select(value => Example.IndexOf($"{firstToken.Prefix}{value.Value}{firstToken.Suffix}{(Separators.Count() > 1 ? Separators.Skip(1).First() : string.Empty)}"))
                .Min(index => index);

            this.Header = headerSize > 0 ? Example.Substring(0, headerSize) : string.Empty;

            var footerStartAt = lastToken.Values
                .Select(value =>
                {
                    var lastTokenString = $"{(Separators.Count() > 1 ? Separators.Skip(1).First() : Separators.First())}{lastToken.Prefix}{value.Value}{lastToken.Suffix}";
                    return Example.LastIndexOf(lastTokenString) + lastTokenString.Length;
                })
                .Max(index => index);

            this.Footer = footerStartAt > 0 ? Example.Substring(footerStartAt) : string.Empty;
        }

        public string Process(string input, Encoding encoding, int limit = 0)
        {
            byte[] byteArray = encoding.GetBytes(input);

            using (var inputStream = new MemoryStream(byteArray))
            {
                using (var outputStream = new MemoryStream())
                {
                    Process(inputStream, outputStream, encoding, limit);

                    outputStream.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(outputStream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        public void Process(Stream input, Stream output, Encoding encoding, int limit = 0)
        {
            Debug.WriteLine($"Processing input...");
            var processTimer = new Stopwatch();
            processTimer.Start();

            Input inputProcessor;

            switch (InputFormat)
            {
                case InputFormat.JSON:
                    inputProcessor = JsonInput.Factory();
                    break;

                case InputFormat.XML:
                    inputProcessor = XmlInput.Factory();
                    break;

                case InputFormat.CSV:
                    inputProcessor = CsvInput.Factory();
                    break;

                case InputFormat.TSV:
                    inputProcessor = TsvInput.Factory();
                    break;

                default:
                    throw new NotImplementedException($"Formating from {InputFormat} not implemented yet");
            }

            Process(
                inputProcessor.TryParse(input, encoding, RecordsArrayPath, limit),
                output,
                encoding);

            processTimer.Stop();
            Debug.WriteLine($"done in {(int)processTimer.Elapsed.TotalMilliseconds}ms");
        }

        private void Process(IEnumerable<IInputRecord> records, Stream output, Encoding encoding)
        {
            using (var writer = new StreamWriter(output, encoding, 1024, true))
            {
                if (!string.IsNullOrEmpty(Header))
                    writer.Write(Header);

                var isFirst = true;
                var _1stOrderSeparator = Separators.First();
                var _2ndOrderSeparator = Separators.Skip(1).FirstOrDefault() ?? string.Empty;
                foreach (var record in records)
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        writer.Write(_1stOrderSeparator);

                    writer.Write(String.Join(_2ndOrderSeparator,
                        Tokens.Select(token => token.GetOutput(record))));
                }

                if (!string.IsNullOrEmpty(Footer))
                    writer.Write(Footer);
            }
        }

        public static unsafe int GetRepeatableHashCode(string s)
        {
            if (string.IsNullOrEmpty(s))
                return 1;

            fixed (char* str = s.ToCharArray())
            {
                char* chPtr = str;
                int num = 0x15051505;
                int num2 = num;
                int* numPtr = (int*)chPtr;
                for (int i = s.Length; i > 0; i -= 4)
                {
                    num = (((num << 5) + num) + (num >> 0x1b)) ^ numPtr[0];
                    if (i <= 2)
                    {
                        break;
                    }
                    num2 = (((num2 << 5) + num2) + (num2 >> 0x1b)) ^ numPtr[1];
                    numPtr += 2;
                }
                return (num + (num2 * 0x5d588b65));
            }
        }

        public static unsafe string GetRepeatableBase64HashCode(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;

            fixed (char* str = s.ToCharArray())
            {
                char* chPtr = str;
                int num = 0x15051505;
                int num2 = num;
                int* numPtr = (int*)chPtr;
                for (int i = s.Length; i > 0; i -= 4)
                {
                    num = (((num << 5) + num) + (num >> 0x1b)) ^ numPtr[0];
                    if (i <= 2)
                    {
                        break;
                    }
                    num2 = (((num2 << 5) + num2) + (num2 >> 0x1b)) ^ numPtr[1];
                    numPtr += 2;
                }
                return Convert.ToBase64String(BitConverter.GetBytes(num + (num2 * 0x5d588b65)));
            }
        }

        public override int GetHashCode()
        {
            return
                new int[] {
                    17,
                    GetRepeatableHashCode(Header),
                    GetRepeatableHashCode(Footer)
                }
                .Concat((Separators ?? EmptyStringArray).Select(separator => GetRepeatableHashCode(separator)))
                .Concat(Tokens != null ?
                    Tokens.Select(token => unchecked(GetRepeatableHashCode(token.InputSelector) * GetRepeatableHashCode(token.OutputSelector))) :
                    EmptyIntArray
                )
                .Select(hash => hash == 0 ? 1 : hash)
                .Aggregate((final, hash) => unchecked(final * hash));
        }

        #region IComparable
        public int CompareTo(object obj)
        {
            if (obj.GetType() != typeof(Formatik))
                return -1;

            return CompareTo((Formatik)obj);
        }

        public int CompareTo(Formatik other)
        {
            var result = this.GetHashCode() - other.GetHashCode();

            if (result == 0)
            {
                var thisAsString =
                    new string[] {
                        Header ?? "",
                        Footer ?? "",
                    }
                    .Concat(Separators ?? EmptyStringArray)
                    .Concat(Tokens != null ?
                        Tokens.Select(token => (token.InputSelector ?? "") + (token.OutputSelector ?? "")) :
                        EmptyStringArray
                    )
                    .Aggregate(new StringBuilder(), (builder, str) => builder.Append(str))
                    .ToString();

                var otherAsString =
                    new string[] {
                        other.Header ?? "",
                        other.Footer ?? "",
                    }
                    .Concat(other.Separators ?? EmptyStringArray)
                    .Concat(other.Tokens != null ?
                        other.Tokens.Select(token => (token.InputSelector ?? "") + (token.OutputSelector ?? "")) :
                        EmptyStringArray
                    )
                    .Aggregate(new StringBuilder(), (builder, str) => builder.Append(str))
                    .ToString();

                return thisAsString.CompareTo(otherAsString);
            }
            else
                return result;
        }

        public bool Equals(Formatik other)
        {
            return CompareTo(other) == 0;
        }
        #endregion
    }
}