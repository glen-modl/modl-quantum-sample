using System;

namespace Modl
{
    public static class UnixTimeExtension
    {
        private static readonly DateTime UNIX_TIME_ZERO_POINT = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Converts a Unix timestamp (UTC timezone by definition) into a DateTime object
        /// </summary>
        /// <param name="value">An input of Unix timestamp in seconds or milliseconds format</param>
        /// <param name="localize">should output be localized or remain in UTC timezone?</param>
        /// <param name="isInMilliseconds">Is input in milliseconds or seconds?</param>
        /// <returns></returns>
        public static DateTime FromUnixTime(this long value, bool localize = false, bool isInMilliseconds = true)
        {
            var result = isInMilliseconds
                ? UNIX_TIME_ZERO_POINT.AddMilliseconds(value)
                : UNIX_TIME_ZERO_POINT.AddSeconds(value);

            if (localize)
            {
                result = result.ToLocalTime();
            }
            
            return result;
        }

        /// <summary>
        /// Converts a DateTime object into a Unix time stamp
        /// </summary>
        /// <param name="value">any DateTime object as input</param>
        /// <param name="isInMilliseconds">Should output be in milliseconds or seconds?</param>
        /// <returns></returns>
        public static long ToUnixTime(this DateTime value, bool isInMilliseconds = true)
        {
            var ret = value.ToUniversalTime().Subtract(UNIX_TIME_ZERO_POINT);
            return (long)(isInMilliseconds
                ? ret.TotalMilliseconds
                : ret.TotalSeconds);
        }
    }
}