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

        public void Clear()
        {
            _books = new Dictionary<string, GncBook>();
            _book = null;
        }

        public void LoadFromFile(string file, string baseCurrency)
        {
            Clear();
            XDocument doc;
            try
            {
                GZipStream gz = new GZipStream(File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read), CompressionMode.Decompress);
                StreamReader sr = new StreamReader(gz);
                doc = XDocument.Load(sr);
                sr.Close();
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

        public GncBook Book
        {
            get { return _book; }
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
