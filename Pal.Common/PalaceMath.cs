using System.Numerics;

namespace Pal.Common
{
    public class PalaceMath
    {
        private static readonly Vector3 ScaleFactor = new(5);

        public static bool IsNearlySamePosition(Vector3 a, Vector3 b)
        {
            a *= ScaleFactor;
            b *= ScaleFactor;
            return (int)a.X == (int)b.X && (int)a.Y == (int)b.Y && (int)a.Z == (int)b.Z;
        }

        public static int GetHashCode(Vector3 v)
        {
            v *= ScaleFactor;
            return HashCode.Combine((int)v.X, (int)v.Y, (int)v.Z);
        }
    }
}
