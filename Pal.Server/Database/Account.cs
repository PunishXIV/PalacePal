using System.ComponentModel.DataAnnotations;

namespace Pal.Server.Database
{
    public class Account
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Anti-Spam: This is a hash of the IP address used to create the account - if you try to create an account later and have the same IP hash
        /// (which should only happen if you have the same IP), this will return the old account id.
        ///
        /// This will be deleted after a set time after account creation.
        /// </summary>
        /// <seealso cref="Pal.Server.Services.RemoveIpHashService"/>
        [MaxLength(20)]
        public string? IpHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();

        public List<SeenLocation> SeenLocations { get; set; } = new();
    }
}
