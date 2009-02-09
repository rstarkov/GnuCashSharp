using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util;

namespace GnuCashSharp
{
    public class GncException: RTException
    {
        public GncException(string message)
            : base(message)
        {
        }

        public GncException(string message, params object[] args)
            : base(message, args)
        {
        }

        public GncException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public GncException(string message, Exception innerException, params object[] args)
            : base(message, innerException, args)
        {
        }
    }
}
