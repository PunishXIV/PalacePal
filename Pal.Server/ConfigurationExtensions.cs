namespace Pal.Server
{
    internal static class ConfigurationExtensions
    {
        public static string GetOrThrow(this IConfiguration configuration, string key)
            => configuration[key] ?? throw new Exception($"no config key {key} defined");
    }
}
