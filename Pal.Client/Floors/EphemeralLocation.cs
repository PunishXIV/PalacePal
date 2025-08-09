namespace Pal.Client.Floors
{
    /// <summary>
    /// This is a currently-visible marker.
    /// </summary>
    internal sealed class EphemeralLocation : MemoryLocation
    {
        public override bool Equals(object? obj) => obj is EphemeralLocation && base.Equals(obj);

        public override int GetHashCode() => base.GetHashCode();

        public static bool operator ==(EphemeralLocation? a, object? b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(EphemeralLocation? a, object? b)
        {
            return !Equals(a, b);
        }

        public override string ToString()
        {
            return $"EphemeralLocation(Position={Position}, Type={Type})";
        }
    }
}
