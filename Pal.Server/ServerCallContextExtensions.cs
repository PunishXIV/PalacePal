using Grpc.Core;
using System.Security.Claims;

namespace Pal.Server
{
    internal static class ServerCallContextExtensions
    {
        public static bool TryGetAccountId(this ServerCallContext context, out Guid accountId)
        {
            accountId = Guid.Empty;
            ClaimsPrincipal? user = context.GetHttpContext()?.User;
            if (user == null)
                return false;

            Claim? claim = user.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
            if (claim == null)
                return false;

            return Guid.TryParse(claim.Value, out accountId);
        }

        public static Guid GetAccountId(this ServerCallContext context)
        {
            if (TryGetAccountId(context, out Guid accountId))
                return accountId;

            throw new InvalidOperationException("No account id in context");
        }
    }
}
