using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Octagon.Formatik
{
    public abstract class Input
    {
        public abstract IEnumerable<IInputRecord> TryParse(Stream input, Encoding encoding, string recordsArrayPath, int limit = 0);
        public abstract (IEnumerable<IInputRecord> Records, string RecordsArrayPath) TryParse(string input, int limit = 0);
    }
}