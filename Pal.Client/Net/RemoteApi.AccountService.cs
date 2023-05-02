using Account;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Pal.Client.Extensions;
using Pal.Client.Properties;
using Pal.Client.Configuration;

namespace Pal.Client.Net
{
    internal partial class RemoteApi
    {
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        private async Task<(bool Success, string Error)> TryConnect(CancellationToken cancellationToken,
            ILoggerFactory? loggerFactory = null, bool retry = true)
        {
            using IDisposable? logScope = _logger.BeginScope("TryConnect");

            var result = await TryConnectImpl(cancellationToken, loggerFactory);
            if (retry && result.ShouldRetry)
                result = await TryConnectImpl(cancellationToken, loggerFactory);

            return (result.Success, result.Error);
        }

        private async Task<(bool Success, string Error, bool ShouldRetry)> TryConnectImpl(
            CancellationToken cancellationToken,
            ILoggerFactory? loggerFactory)
        {
            if (_configuration.Mode != EMode.Online)
            {
                _logger.LogDebug("Not Online, not attempting to establish a connection");
                return (false, Localization.ConnectionError_NotOnline, false);
            }

            if (_channel == null ||
                !(_channel.State == ConnectivityState.Ready || _channel.State == ConnectivityState.Idle))
            {
                Dispose();

                _logger.LogInformation("Creating new gRPC channel");
                _channel = GrpcChannel.ForAddress(RemoteUrl, new GrpcChannelOptions
                {
                    HttpHandler = new SocketsHttpHandler
                    {
                        ConnectTimeout = TimeSpan.FromSeconds(5),
                        SslOptions = GetSslClientAuthenticationOptions(),
                    },
                    LoggerFactory = loggerFactory,
                });

                _logger.LogInformation("Connecting to upstream service at {Url}", RemoteUrl);
                await _channel.ConnectAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogTrace("Acquiring connect lock");
            await _connectLock.WaitAsync(cancellationToken);
            _logger.LogTrace("Obtained connect lock");

            try
            {
                var accountClient = new AccountService.AccountServiceClient(_channel);
                IAccountConfiguration? configuredAccount = _configuration.FindAccount(RemoteUrl);
                if (configuredAccount == null)
                {
                    _logger.LogInformation("No account information saved for {Url}, creating new account", RemoteUrl);
                    var createAccountReply = await accountClient.CreateAccountAsync(new CreateAccountRequest(),
                        headers: UnauthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10),
                        cancellationToken: cancellationToken);
                    if (createAccountReply.Success)
                    {
                        if (!Guid.TryParse(createAccountReply.AccountId, out Guid accountId))
                            throw new InvalidOperationException("invalid account id returned");

                        configuredAccount = _configuration.CreateAccount(RemoteUrl, accountId);
                        _logger.LogInformation("Account created with id {AccountId}", accountId.ToPartialId());

                        _configurationManager.Save(_configuration);
                    }
                    else
                    {
                        _logger.LogError("Account creation failed with error {Error}", createAccountReply.Error);
                        if (createAccountReply.Error == CreateAccountError.UpgradeRequired && !_warnedAboutUpgrade)
                        {
                            _chat.Error(Localization.ConnectionError_OldVersion);
                            _warnedAboutUpgrade = true;
                        }

                        return (false,
                            string.Format(Localization.ConnectionError_CreateAccountFailed, createAccountReply.Error),
                            false);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (configuredAccount == null)
                {
                    _logger.LogWarning("No account to login with");
                    return (false, Localization.ConnectionError_CreateAccountReturnedNoId, false);
                }

                if (!_loginInfo.IsValid)
                {
                    _logger.LogInformation("Logging in with account id {AccountId}",
                        configuredAccount.AccountId.ToPartialId());
                    LoginReply loginReply = await accountClient.LoginAsync(
                        new LoginRequest { AccountId = configuredAccount.AccountId.ToString() },
                        headers: UnauthorizedHeaders(), deadline: DateTime.UtcNow.AddSeconds(10),
                        cancellationToken: cancellationToken);

                    if (loginReply.Success)
                    {
                        _logger.LogInformation("Login successful with account id: {AccountId}",
                            configuredAccount.AccountId.ToPartialId());
                        _loginInfo = new LoginInfo(loginReply.AuthToken);

                        bool save = configuredAccount.EncryptIfNeeded();

                        List<string> newRoles = _loginInfo.Claims?.Roles.ToList() ?? new();
                        if (!newRoles.SequenceEqual(configuredAccount.CachedRoles))
                        {
                            configuredAccount.CachedRoles = newRoles;
                            save = true;
                        }

                        if (save)
                            _configurationManager.Save(_configuration);
                    }
                    else
                    {
                        _logger.LogError("Login failed with error {Error}", loginReply.Error);
                        _loginInfo = new LoginInfo(null);
                        if (loginReply.Error == LoginError.InvalidAccountId)
                        {
                            _configuration.RemoveAccount(RemoteUrl);
                            _configurationManager.Save(_configuration);

                            _logger.LogInformation("Attempting connection retry without account id");
                            return (false, Localization.ConnectionError_InvalidAccountId, true);
                        }

                        if (loginReply.Error == LoginError.UpgradeRequired && !_warnedAboutUpgrade)
                        {
                            _chat.Error(Localization.ConnectionError_OldVersion);
                            _warnedAboutUpgrade = true;
                        }

                        return (false, string.Format(Localization.ConnectionError_LoginFailed, loginReply.Error),
                            false);
                    }
                }

                if (!_loginInfo.IsValid)
                {
                    _logger.LogError("Login state is loggedIn={LoggedIn}, expired={Expired}", _loginInfo.IsLoggedIn,
                        _loginInfo.IsExpired);
                    return (false, Localization.ConnectionError_LoginReturnedNoToken, false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return (true, string.Empty, false);
            }
            finally
            {
                _logger.LogTrace("Releasing connectLock");
                _connectLock.Release();
            }
        }

        private async Task<bool> Connect(CancellationToken cancellationToken)
        {
            var result = await TryConnect(cancellationToken);
            return result.Success;
        }

        public async Task<string> VerifyConnection(CancellationToken cancellationToken = default)
        {
            using IDisposable? logScope = _logger.BeginScope("VerifyConnection");

            _warnedAboutUpgrade = false;

            var connectionResult = await TryConnect(cancellationToken, loggerFactory: _loggerFactory);
            if (!connectionResult.Success)
                return string.Format(Localization.ConnectionError_CouldNotConnectToServer, connectionResult.Error);

            _logger.LogInformation("Connection established, trying to verify auth token");
            var accountClient = new AccountService.AccountServiceClient(_channel);
            await accountClient.VerifyAsync(new VerifyRequest(), headers: AuthorizedHeaders(),
                deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: cancellationToken);

            _logger.LogInformation("Verification returned no errors.");
            return Localization.ConnectionSuccessful;
        }

        internal sealed class LoginInfo
        {
            public LoginInfo(string? authToken)
            {
                if (!string.IsNullOrEmpty(authToken))
                {
                    IsLoggedIn = true;
                    AuthToken = authToken;
                    Claims = JwtClaims.FromAuthToken(authToken);
                }
                else
                    IsLoggedIn = false;
            }

            public bool IsLoggedIn { get; }
            public string? AuthToken { get; }
            public JwtClaims? Claims { get; }

            private DateTimeOffset ExpiresAt =>
                Claims?.ExpiresAt.Subtract(TimeSpan.FromMinutes(5)) ?? DateTimeOffset.MinValue;

            public bool IsExpired => ExpiresAt < DateTimeOffset.UtcNow;

            public bool IsValid => IsLoggedIn && !IsExpired;
        }
    }
}
