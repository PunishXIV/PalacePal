using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pal.Server.Database;
using Pal.Server.Services;

namespace Pal.Server
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddCustomConfiguration();
            builder.Services.AddGrpc(o => o.EnableDetailedErrors = true);
            builder.Services.AddSingleton<PalConnectionInterceptor>();
            builder.Services.AddDbContext<PalServerContext>((serviceProvider, o) =>
            {
                if (builder.Configuration["DataDirectory"] is { } dbPath)
                {
                    dbPath += "/palace-pal.db";
                }
                else
                {
#if DEBUG
                    dbPath = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "pal.db");
#else
                    dbPath = "palace-pal.db";
#endif
                }

                o.UseSqlite($"Data Source={dbPath}");
                o.AddInterceptors(serviceProvider.GetRequiredService<PalConnectionInterceptor>());
            });
            builder.Services.AddHostedService<RemoveIpHashService>();
            builder.Services.AddSingleton<PalaceLocationCache>();
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                var jwtConfig = builder.Configuration.GetRequiredSection("JWT").Get<JwtConfiguration>() ??
                                throw new ArgumentException("no JWT config");
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtConfig.Issuer,
                    ValidAudience = jwtConfig.Audience,
                    IssuerSigningKey = jwtConfig.ToSecurityKey(),
                };
            });
            builder.Services.AddAuthorization();

            if (builder.Configuration["DataDirectory"] is { } dataDirectory)
            {
                builder.Services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(dataDirectory));
            }

            builder.Host.UseSystemd();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGrpcService<AccountService>();
            app.MapGrpcService<PalaceService>();
            app.MapGrpcService<ExportService>();

            using (var scope = app.Services.CreateScope())
            {
                await using var dbContext = scope.ServiceProvider.GetRequiredService<PalServerContext>();
                await dbContext.Database.MigrateAsync();
            }

            await app.RunAsync();
        }
    }
}
