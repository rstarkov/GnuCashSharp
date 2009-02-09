using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp
{
    public class GncSession
    {
        private Dictionary<string, GncBook> _books;
        private GncBook _book;

        public void Clear()
        {
            _books = new Dictionary<string, GncBook>();
            _book = null;
        }

        public void LoadFromFile(string file)
        {
            Clear();
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (Exception E) { throw new GncException("Cannot parse XML file: " + E.Message); }
            if (doc.Root.Name != "gnc-v2")
                throw new GncException("Cannot load file: root node name is not \"gnc-v2\"");

            foreach (var el in doc.Root.Elements(GncName.Gnc("book")))
            {
                GncBook book = new GncBook(this, el);
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
    }
}
