using System;

namespace Pal.Client.Extensions
{
    public static class GuidExtensions
    {
        public static string ToPartialId(this Guid g, int length = 13)
            => g.ToString().ToPartialId();

        public static string ToPartialId(this string s, int length = 13)
            => s.PadRight(length + 1).Substring(0, length);
    }
}
