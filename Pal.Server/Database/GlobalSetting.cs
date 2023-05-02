using System.ComponentModel.DataAnnotations;

namespace Pal.Server.Database
{
    public sealed class GlobalSetting
    {
        public GlobalSetting(string key, string value)
        {
            Key = key;
            Value = value;
        }

        [Key]
        public string Key { get; set; }

        [MaxLength(128)]
        public string Value { get; set; }
    }
}
