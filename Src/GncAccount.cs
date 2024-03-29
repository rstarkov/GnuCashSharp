﻿using System.Text;
using System.Xml.Linq;
using RT.Util.ExtensionMethods;

namespace GnuCashSharp;

public class GncAccount : IComparable<GncAccount>
{
    private GncBook _book;
    private string _name;
    private string _guid;
    private string _parentGuid;
    private GncCommodity _commodity;
    private int _commodityScu, _commodityDecimals;
    private string _description;

    public GncAccount(GncBook book)
    {
        _book = book;
    }

    public GncAccount(GncBook book, XElement xml)
        : this(book)
    {
        _name = xml.ChkElement(GncName.Act("name")).Value;
        _guid = xml.ChkElement(GncName.Act("id")).Value;
        _parentGuid = xml.ValueOrDefault(GncName.Act("parent"), (string) null);
        _commodity = book.GetCommodity(GncCommodity.MakeIdentifier(xml.Element(GncName.Act("commodity"))));
        if (_commodity == null && xml.Element(GncName.Act("type"))?.Value == "ROOT")
            _commodity = _book.BaseCurrency;
        string scu = xml.ValueOrDefault(GncName.Act("commodity-scu"), "1");
        _commodityDecimals = scu.Count(c => c == '0');
        if (scu != "1" + new string('0', _commodityDecimals))
            throw new Exception("Could not parse commodity-scu: expected a power of 10.");
        _commodityScu = int.Parse(scu);
        _description = xml.ValueOrDefault(GncName.Act("description"), (string) null);
    }

    public GncBook Book
    {
        get { return _book; }
    }

    public string Name
    {
        get { return _name; }
    }

    public string Guid
    {
        get { return _guid; }
    }

    public string ParentGuid
    {
        get { return _parentGuid; }
    }

    public GncCommodity Commodity
    {
        get { return _commodity; }
    }

    /// <summary>
    /// Indicates commodity subdivision unit. "1" means whole units. "100" means "subdivide into hundredths".
    /// </summary>
    public int CommodityScu
    {
        get { return _commodityScu; }
    }

    public string Description
    {
        get { return _description; }
    }

    public GncAccount Parent
    {
        get { return _book.GetAccount(_parentGuid); }
    }

    /// <summary>
    /// The depth of the root account is 0. The depth of its immediate children is 1. Etc.
    /// </summary>
    public int Depth
    {
        get
        {
            int depth = 0;
            var acct = this;
            while (acct.Parent != null)
            {
                depth++;
                acct = acct.Parent;
            }
            return depth;
        }
    }

    public IEnumerable<GncAccount> EnumChildren()
    {
        foreach (var child in Book.AccountEnumChildren(this))
            yield return child;
    }

    /// <summary>
    /// Enumerates all splits in this account, and optionally, in subaccounts.
    /// The splits will be ordered correctly if only this account's splits are
    /// enumerated.
    /// </summary>
    public IEnumerable<GncSplit> EnumSplits(bool subAccts)
    {
        if (subAccts)
        {
            foreach (var split in _book.AccountEnumSplits(this))
                yield return split;
            foreach (var acct in EnumChildren())
                foreach (var split in acct.EnumSplits(true))
                    yield return split;
        }
        else
        {
            foreach (var split in _book.AccountEnumSplits(this))
                yield return split;
        }
    }

    public GncCommodityAmount GetBalance(DateTime asOf)
    {
        var result = new GncCommodityAmount(0, Commodity);
        foreach (var split in EnumSplits(false).Where(spl => spl.Transaction.DatePosted <= asOf))
            result += split.Amount;
        return result.WithTimepoint(asOf);
    }

    public GncMultiAmount GetBalanceWithSubaccounts(DateTime asOf)
    {
        var result = new GncMultiAmount();
        foreach (var split in EnumSplits(true).Where(spl => spl.Transaction.DatePosted <= asOf))
            result.AddInplace(split.Amount);
        result.Timepoint = asOf;
        return result;
    }

    public GncCommodityAmount GetBalanceConverted(DateTime asOf, bool withSubaccts, GncCommodity convertTo)
    {
        var result = new GncCommodityAmount(0, convertTo);
        foreach (var split in EnumSplits(withSubaccts).Where(spl => spl.Transaction.DatePosted <= asOf))
            result += split.AmountConverted(convertTo);
        return result.WithTimepoint(asOf);
    }

    public GncCommodityAmount GetTotal(DateInterval interval)
    {
        var result = new GncCommodityAmount(0, Commodity);
        foreach (var split in EnumSplits(false).Where(spl => interval.Contains(spl.Transaction.DatePosted)))
            result += split.Amount;
        return result;
    }

    public GncMultiAmount GetTotalWithSubaccounts(DateInterval interval)
    {
        var result = new GncMultiAmount();
        foreach (var split in EnumSplits(true).Where(spl => interval.Contains(spl.Transaction.DatePosted)))
            result.AddInplace(split.Amount);
        return result;
    }

    public GncCommodityAmount GetTotalConverted(DateInterval interval, bool withSubaccts, GncCommodity convertTo)
    {
        var result = new GncCommodityAmount(0, convertTo);
        foreach (var split in EnumSplits(withSubaccts).Where(spl => interval.Contains(spl.Transaction.DatePosted)))
            result += split.AmountConverted(convertTo);
        return result;
    }

    public string Path(string separator)
    {
        if (this == _book.AccountRoot)
            return "";
        StringBuilder sb = new StringBuilder(_name);
        var acct = this.Parent;
        while (acct != _book.AccountRoot)
        {
            sb.Insert(0, acct.Name + separator);
            acct = acct.Parent;
        }
        return sb.ToString();
    }

    public List<GncAccount> PathAsList()
    {
        return PathAsList(_book.AccountRoot);
    }

    public List<GncAccount> PathAsList(GncAccount acctBase)
    {
        var result = new List<GncAccount>();
        var acct = this;
        while (acct != acctBase)
        {
            result.Insert(0, acct);
            acct = acct.Parent;
        }
        return result;
    }

    public int CompareTo(GncAccount other)
    {
        return this._name.CompareTo(other._name);
    }

    public decimal RoundQuantity(decimal quantity)
    {
        return Math.Round(quantity, _commodityDecimals);
    }

    public bool RoundedEquals(decimal qty1, decimal qty2)
    {
        return Math.Floor(qty1 * _commodityScu) == Math.Floor(qty2 * _commodityScu)
            || Math.Ceiling(qty1 * _commodityScu) == Math.Ceiling(qty2 * _commodityScu);
    }
}
