using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Octagon.Formatik
{
    public class XmlInputRecord : IInputRecord
    {
        private static readonly string[] emptyStringArray = new string[0];
        private static readonly Token[] emptyTokenArray = new Token[0];

        private XElement recordNode;


        public int Index { get; }

        public string GetXPath(XElement element)
        {
            if (element == null)
            {
                throw new ArgumentNullException("element");
            }

            Func<XElement, string> relativeXPath = e =>
            {
                int index = IndexPosition(e);
                string name = e.Name.LocalName;

                // If the element is the root, no index is required
                return (index == -1) ? name : $"{name}[{index}]";
            };

            if (element != recordNode)
            {
                var ancestors = element.Ancestors()
                    .TakeWhile(p => p != recordNode)
                    .Select(e => relativeXPath(e))
                    .Reverse()
                    .Append(relativeXPath(element))
                    .ToArray();

                return string.Join("/", ancestors);
            }
            else
                return null;
        }

        /// <summary>
        /// Get the index of the given XElement relative to its
        /// siblings with identical names. If the given element is
        /// the root, -1 is returned.
        /// </summary>
        /// <param name="element">
        /// The element to get the index of.
        /// </param>
        private static int IndexPosition(XElement element)
        {
            if (element == null)
                throw new ArgumentNullException("element");

            if (element.Parent == null)
                return -1;

            int i = 1; // Indexes for nodes start at 1, not 0

            foreach (var sibling in element.Parent.Elements(element.Name))
            {
                if (sibling == element)
                    return i;

                i++;
            }

            throw new InvalidOperationException("element has been removed from its parent.");
        }


        public XmlInputRecord()
        {

        }

        public XmlInputRecord(XElement recordNode, int index)
        {
            this.recordNode = recordNode;
            this.Index = index;
        }

        public string GetToken(string tokenSelector)
        {
            var values = ((IEnumerable<object>)recordNode.XPathEvaluate(tokenSelector));

            if (tokenSelector.EndsWith("/text()"))
            {
                return values != null ?
                    string.Concat(values.Cast<XText>().Distinct().Select(t => t.Value)) :
                    null;
            }
            else if (Regex.IsMatch(tokenSelector, "@[^/]+$"))
            {
                return values != null && values.Any() ?
                    ((XAttribute)values.First()).Value :
                    null;
            }
            else
                throw new FormatikException($"[{this.GetType().Name}] Unrecognized token selector '{tokenSelector}'");
        }

        private int index;

        private IEnumerable<Token> GetTokens(XObject node)
        {
            if (node is XElement)
            {
                var tokens = new List<Token>();
                var element = (XElement)node;

                tokens.AddRange(element.Attributes()
                    .SelectMany(a => GetTokens(a)));

                tokens.AddRange(element.Nodes()
                    .SelectMany(n => GetTokens(n)));

                return tokens;
            }
            else if (node is XAttribute)
            {
                var attr = (XAttribute)node;
                var val = attr.Value;
                var xpath = GetXPath(attr.Parent);

                return !string.IsNullOrEmpty(val) ?
                    new Token[] { new Token(
                        this,
                        val,
                        $"{(xpath == null ? "" : xpath + "/")}@{attr.Name}",
                        index++) } :
                    emptyTokenArray;
            }
            else if (node is XText)
            {
                var text = (XText)node;
                var val = text.Value;
                var xpath = GetXPath(text.Parent);

                return !string.IsNullOrEmpty(val) ?
                    new Token[] { new Token(
                        this,
                        val,
                        $"{(xpath == null ? "" : xpath + "/")}text()",
                        index++) } :
                    emptyTokenArray;
            }
            else
                return emptyTokenArray;
        }

        public IEnumerable<Token> GetTokens()
        {
            index = 0;
            return GetTokens(recordNode);
        }
    }
}