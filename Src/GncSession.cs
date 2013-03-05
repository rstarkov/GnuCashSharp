using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;
using System.IO.Compression;
using System.IO;

namespace GnuCashSharp
{
    public class GncSession
    {
        private Dictionary<string, GncBook> _books;
        private GncBook _book;
        private List<string> _warnings = new List<string>();
        private DateTime _modifiedTimestamp;
        private string _balsnapPrefix;

        public void Clear()
        {
            _books = new Dictionary<string, GncBook>();
            _book = null;
        }

        public void LoadFromFile(string file, string baseCurrency, string balsnapPrefix)
        {
            Clear();
            _balsnapPrefix = balsnapPrefix;

            XDocument doc;

            _modifiedTimestamp = File.GetLastWriteTimeUtc(file);

            try
            {
                using (var gz = new GZipStream(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read), CompressionMode.Decompress))
                using (var sr = new StreamReader(gz))
                    doc = XDocument.Load(sr);
            }
            catch
            {
                try { doc = XDocument.Load(file); }
                catch (Exception E) { throw new GncException("Cannot parse XML file: " + E.Message); }
            }

            if (doc.Root.Name != "gnc-v2")
                throw new GncException("Cannot load file: root node name is not \"gnc-v2\"");

            foreach (var el in doc.Root.Elements(GncName.Gnc("book")))
            {
                GncBook book = new GncBook(this, el, baseCurrency);
                _books.Add(book.Guid, book);
                if (Book == null)
                    _book = book;
                else
                    throw new GncException("Multiple books in the file; this is currently entirely untested.");
            }
        }

        public string BalsnapPrefix
        {
            get { return _balsnapPrefix; }
        }

        public GncBook Book
        {
            get { return _book; }
        }

        public DateTime ModifiedTimestamp
        {
            get { return _modifiedTimestamp; }
        }

        public void Warn(string warning)
        {
            _warnings.Add(warning);
        }

        public IEnumerable<string> EnumWarnings()
        {
            foreach (var warning in _warnings)
                yield return warning;
        }

        public void ClearWarnings()
        {
            _warnings.Clear();
        }
    }
}
