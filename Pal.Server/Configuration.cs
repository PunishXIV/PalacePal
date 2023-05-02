using Microsoft.IdentityModel.Tokens;

namespace Pal.Server
{
    public sealed class JwtConfiguration
    {
        public required string Key { get; set; }
        public required string Issuer { get; set; }
        public required string Audience { get; set; }

        public SymmetricSecurityKey ToSecurityKey() => new(Convert.FromBase64String(Key));
    }

    internal sealed class CustomConfigurationProvider : ConfigurationProvider
    {
        private readonly string _dataDirectory;

        public CustomConfigurationProvider(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
        }

        public override void Load()
        {
            var jwtKeyPath = Path.Join(_dataDirectory, "jwt.key");
            if (File.Exists(jwtKeyPath))
                Data["JWT:Key"] = File.ReadAllText(jwtKeyPath);
        }
    }

    internal sealed class CustomConfigurationSource : IConfigurationSource
    {

        private readonly string _dataDirectory;

        public CustomConfigurationSource(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new CustomConfigurationProvider(_dataDirectory);
    }

    internal static class ConfigurationBuilderExtensions
    {
        public static IConfigurationBuilder AddCustomConfiguration(this IConfigurationBuilder builder)
        {
            var tempConfig = builder.Build();
            if (tempConfig["DataDirectory"] is { } dataDirectory)
                return builder.Add(new CustomConfigurationSource(dataDirectory));
            else
                return builder;
        }
    }
}
