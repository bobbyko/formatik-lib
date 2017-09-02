using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Octagon.Formatik
{
    public class XmlInput: Input
    {
        private static Input instance;

        public static Input Factory() {
            if (instance == null)
                instance = new XmlInput();

            return instance;
        }
        
        private static void FindRecordNode(XElement node, ref XElement largestNode)
        {
            if (node.Elements().Take(3).Count() == 3 &&
                (largestNode == null || node.Elements().Count() > largestNode.Elements().Count()))
            {
                largestNode = node;
            }
            else
                foreach (var child in node.Elements())
                    FindRecordNode(child, ref largestNode);
        }

        public override IEnumerable<IInputRecord> TryParse(Stream input, Encoding encoding, string recordsArrayPath, int limit = 0)
        {
            using (var reader = new StreamReader(input, encoding))
            {
                var document = XDocument.Load(reader);

                var records = string.IsNullOrEmpty(recordsArrayPath) ?
                    document.Root :
                    document.Root.XPathSelectElement(recordsArrayPath);

                return records.Elements()
                    .Take(limit > 0 ? limit : int.MaxValue)
                    .Select((node, i) => new XmlInputRecord(node, i))
                    .ToArray();
            }
        }
        
        public override (IEnumerable<IInputRecord> Records, string RecordsArrayPath) TryParse(string input, int limit = 0)
        {
            if (Regex.IsMatch(input, "^<\\?xml"))
            {
                var doc = XDocument.Parse(input);

                XElement recordsNode = null;
                FindRecordNode(doc.Root, ref recordsNode);

                // we will use this dummy instance to get the XPath of the recordsNode reletive to the root
                var dummyRec = new XmlInputRecord(doc.Root, 0);

                return (
                    recordsNode.Elements()
                        .Select((rec, i) => new XmlInputRecord(rec, i))
                        .Take(limit > 0 ? limit : int.MaxValue)
                        .ToArray(),
                    recordsNode != recordsNode.Document.Root ? 
                        "//" + dummyRec.GetXPath(recordsNode) : 
                        null
                );
            }

            return (null, null);
        }
    }
}