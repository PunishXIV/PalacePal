using System.ComponentModel.DataAnnotations;
using Pal.Client.Extensions;
using Pal.Client.Net;

namespace Pal.Client.Database
{
    /// <summary>
    /// To avoid sending too many requests to the server, we cache which locations have been seen
    /// locally. These never expire, and locations which have been seen with a specific account
    /// are never sent to the server again.
    ///
    /// To be marked as seen, it needs to be essentially processed by <see cref="RemoteApi.MarkAsSeen"/>.
    /// </summary>
    internal sealed class RemoteEncounter
    {
        [Key]
        public int Id { get; private set; }

        public int ClientLocationId { get; private set; }
        public ClientLocation ClientLocation { get; private set; } = null!;

        /// <summary>
        /// Partial account id. This is partially unique - however problems would (in theory)
        /// only occur once you have two account-ids where the first 13 characters are equal.
        /// </summary>
        [MaxLength(13)]
        public string AccountId { get; private set; }

        private RemoteEncounter(int clientLocationId, string accountId)
        {
            ClientLocationId = clientLocationId;
            AccountId = accountId;
        }

        public RemoteEncounter(ClientLocation clientLocation, string accountId)
        {
            ClientLocation = clientLocation;
            AccountId = accountId.ToPartialId();
        }
    }
}
