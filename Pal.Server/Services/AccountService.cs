using Account;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Pal.Server.Database;
using static Account.AccountService;

namespace Pal.Server.Services
{
    internal sealed class AccountService : AccountServiceBase
    {
        private readonly ILogger<AccountService> _logger;
        private readonly PalServerContext _dbContext;
        private readonly string _tokenIssuer;
        private readonly string _tokenAudience;
        private readonly bool _useForwardedIp;
        private readonly SymmetricSecurityKey _signingKey;

        private byte[]? _salt;

        public AccountService(ILogger<AccountService> logger, IConfiguration configuration, PalServerContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;

            var jwtConfig = configuration.GetRequiredSection("JWT").Get<JwtConfiguration>() ?? throw new ArgumentException("no JWT config");
            _tokenIssuer = jwtConfig.Issuer;
            _tokenAudience = jwtConfig.Audience;
            _useForwardedIp = bool.Parse(configuration.GetOrThrow("UseForwardedIp"));
            _signingKey = jwtConfig.ToSecurityKey();
        }

        [AllowAnonymous]
        public override async Task<CreateAccountReply> CreateAccount(CreateAccountRequest request, ServerCallContext context)
        {
            try
            {
                var remoteIp = context.GetHttpContext().Connection.RemoteIpAddress;
                if (_useForwardedIp)
                {
                    remoteIp = null;
                    foreach (var header in context.RequestHeaders)
                    {
                        if (header.Key == "x-real-ip")
                        {
                            remoteIp = IPAddress.Parse(header.Value);
                            break;
                        }
                    }
                }
                if (remoteIp == null)
                    return new CreateAccountReply { Success = false, Error = CreateAccountError.InvalidHash };

                _salt ??= Convert.FromBase64String((await _dbContext.GlobalSettings.FindAsync(new object[] { "salt" }, cancellationToken: context.CancellationToken))!.Value);
                var ipHash = Convert.ToBase64String(new Rfc2898DeriveBytes(remoteIp.GetAddressBytes(), _salt, iterations: 10000, HashAlgorithmName.SHA1).GetBytes(24));

                Database.Account? existingAccount = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.IpHash == ipHash, cancellationToken: context.CancellationToken);
                if (existingAccount != null)
                {
                    _logger.LogInformation("CreateAccount: Returning existing account {AccountId} for ip hash {IpHash} ({Ip})", existingAccount.Id, ipHash, remoteIp.ToString().Substring(0, Math.Min(5, remoteIp.ToString().Length)));
                    return new CreateAccountReply { Success = true, AccountId = existingAccount.Id.ToString() };
                }


                Database.Account newAccount = new Database.Account
                {
                    Id = Guid.NewGuid(),
                    IpHash = ipHash,
                    CreatedAt = DateTime.Now,
                };
                _dbContext.Accounts.Add(newAccount);
                await _dbContext.SaveChangesAsync(context.CancellationToken);

                _logger.LogInformation("CreateAccount: Created new account {AccountId} for ip hash {IpHash}", newAccount.Id, ipHash);
                return new CreateAccountReply
                {
                    Success = true,
                    AccountId = newAccount.Id.ToString(),
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not create account: {e}", e);
                return new CreateAccountReply { Success = false, Error = CreateAccountError.Unknown };
            }
        }

        [AllowAnonymous]
        public override async Task<LoginReply> Login(LoginRequest request, ServerCallContext context)
        {
            try
            {
                if (!Guid.TryParse(request.AccountId, out Guid accountId))
                {
                    _logger.LogWarning("Submitted account id '{AccountId}' is not a valid id", request.AccountId);
                    return new LoginReply { Success = false, Error = LoginError.Unknown };
                }

                var existingAccount = await _dbContext.Accounts.FindAsync(new object[] { accountId }, cancellationToken: context.CancellationToken);
                if (existingAccount == null)
                {
                    _logger.LogWarning("Could not find account with id '{AccountId}'", accountId);
                    return new LoginReply { Success = false, Error = LoginError.InvalidAccountId };
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, accountId.ToString()),
                    new(ClaimTypes.Role, "default"),
                };

                foreach (var role in existingAccount.Roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));

                var tokenHandler = new JwtSecurityTokenHandler();
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims.ToArray()),
                    Expires = DateTime.Now.AddDays(1),
                    Issuer = _tokenIssuer,
                    Audience = _tokenAudience,
                    SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256Signature),
                };

                return new LoginReply
                {
                    Success = true,
                    AuthToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor)),
                };
            }
            catch (Exception e)
            {
                _logger.LogError("Could not log into account {Account}: {e}", request.AccountId, e);
                return new LoginReply { Success = false, Error = LoginError.Unknown };
            }
        }

        [Authorize]
        public override Task<VerifyReply> Verify(VerifyRequest request, ServerCallContext context)
        {
            var _ = context.GetAccountId();
            return Task.FromResult(new VerifyReply());
        }
    }
}
