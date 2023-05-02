namespace Pal.Client.Configuration
{
    public enum EMode
    {
        /// <summary>
        /// Fetches trap locations from remote server.
        /// </summary>
        Online = 1,

        /// <summary>
        /// Only shows traps found by yourself uisng a pomander of sight.
        /// </summary>
        Offline = 2,
    }
}
