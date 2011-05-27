using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp
{
    public class GncCommodity
    {
        private GncBook _book;
        private string _identifier;
        private string _space;
        private string _name;
        private GncTimeSeries _exrate;

        public GncCommodity(GncBook book, string identifier = null, string space = null, string name = null)
        {
            _book = book;
            _identifier = identifier;
            _space = space;
            _name = name;
            _exrate = new GncTimeSeries();
        }

        public GncCommodity(GncBook book, XElement xml)
            : this(book)
        {
            _space = xml.ChkElement(GncName.Cmdty("space")).Value;
            _name = xml.ChkElement(GncName.Cmdty("id")).Value;
            _identifier = MakeIdentifier(_space, _name);
        }

        public GncBook Book
        {
            get { return Book; }
        }

        public string Identifier
        {
            get { return _identifier; }
        }

        public string Space
        {
            get { return _space; }
        }

        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Gets the time series of the exchange rate of this currency into the
        /// book's base currency. The rate is the value of 1 unit of commodity
        /// in terms of the base currency.
        /// </summary>
        public GncTimeSeries ExRate
        {
            get { return _exrate; }
        }

        public bool IsBaseCurrency
        {
            get { return _identifier == _book.BaseCurrencyId; }
        }

        public static string MakeIdentifier(XElement xml)
        {
            if (xml == null)
                return null;
            else
                return MakeIdentifier(xml.ChkElement(GncName.Cmdty("space")).Value, xml.ChkElement(GncName.Cmdty("id")).Value);
        }

        public static string MakeIdentifier(string space, string name)
        {
            return space == "ISO4217" ? name : (space + ":" + name);
        }
    }
}
