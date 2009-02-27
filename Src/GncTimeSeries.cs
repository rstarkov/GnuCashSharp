using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Collections;

namespace GnuCashSharp
{
    public class GncTimeSeries: IEnumerable<KeyValuePair<DateTime, decimal>>
    {
        private SortedList<DateTime, decimal> _data = new SortedList<DateTime, decimal>();

        public decimal this[DateTime time]
        {
            get
            {
                return Get(time, GncInterpolation.None);
            }
            set
            {
                if (time.Kind != DateTimeKind.Utc)
                    throw new RTException("DateTime passed to GncTimeSeries[] must be a UTC time.");
                if (_data.ContainsKey(time))
                    _data[time] = value;
                else
                    _data.Add(time, value);
            }
        }

        /// <summary>
        /// Retrieves the value of the series at a specified point in time. See Remarks for
        /// info on interpolation modes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When the specified key does not exist in the data set, <see cref="Get"/> will
        /// interpolate or extrapolate as necessary. This can be prevented by using interpolation
        /// mode "None", in which case an exception will be thrown instead.
        /// </para>
        /// <para>
        /// If extrapolation is required, this method will always extrapolate to the nearest
        /// available value. For example, the series "5, 8, 12" would be extrapolated as "5, 5, 5, 8, 12, 12, 12".
        /// </para>
        /// </remarks>
        /// <param name="time"></param>
        /// <param name="interpolation"></param>
        public decimal Get(DateTime time, GncInterpolation interpolation)
        {
            if (time.Kind != DateTimeKind.Utc)
                throw new RTException("DateTime passed to GncTimeSeries.Get must be a UTC time.");

            if (_data.Count == 0)
                throw new InvalidOperationException("Cannot Get a value from an empty GncTimeSeries.");

            if (interpolation == GncInterpolation.None)
                return _data[time];           // Throws if not found, as desired for this interpolation type

            int index1, index2;
            _data.BinarySearch(time, out index1, out index2);

            if (index1 == index2)
                return _data.Values[index1];  // Found the key we're looking for
            if (index1 == int.MinValue)
                return _data.Values[index2];  // Must extrapolate to the left
            if (index2 == int.MaxValue)
                return _data.Values[index1];  // Must extrapolate to the right

            if (interpolation == GncInterpolation.NearestBefore)
            {
                return _data.Values[index1];
            }
            else if (interpolation == GncInterpolation.NearestAfter)
            {
                return _data.Values[index2];
            }
            else if (interpolation == GncInterpolation.Nearest)
            {
                var spanL = time - _data.Keys[index1];
                var spanR = _data.Keys[index2] - time;
                return (spanL <= spanR) ? _data.Values[index1] : _data.Values[index2];
            }
            else if (interpolation == GncInterpolation.Linear)
            {
                decimal spanL = (decimal)(time - _data.Keys[index1]).Ticks;
                decimal spanR = (decimal)(_data.Keys[index2] - time).Ticks;
                decimal valL = _data.Values[index1];
                decimal valR = _data.Values[index2];
                return valL + (valR - valL) * spanL / (spanL + spanR);
            }

            throw new NotImplementedException(); // if new interpolation modes are added
        }

        public IEnumerator<KeyValuePair<DateTime, decimal>> GetEnumerator()
        {
            foreach (var pt in _data)
                yield return pt;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            foreach (var pt in _data)
                yield return pt;
        }
    }
}
