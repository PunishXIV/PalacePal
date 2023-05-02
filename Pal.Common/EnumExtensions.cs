using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Pal.Common
{
    public static class EnumExtensions
    {
        public static int? GetOrder(this Enum e)
        {
            Type type = e.GetType();
            MemberInfo field = type.GetMember(e.ToString()).Single();
            DisplayAttribute? attribute = field.GetCustomAttributes(typeof(DisplayAttribute), false).Cast<DisplayAttribute>().FirstOrDefault();
            return attribute?.Order;
        }
    }
}
