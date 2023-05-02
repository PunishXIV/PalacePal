namespace Pal.Server.Database
{
    public sealed class ServerLocation
    {
        public Guid Id { get; set; }
        public ushort TerritoryType { get; set; }
        public EType Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Guid AccountId { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<SeenLocation> SeenLocations { get; set; } = new();

        public enum EType
        {
            Trap = 1,
            Hoard = 2,
            Debug = 3,
        }
    }
}
