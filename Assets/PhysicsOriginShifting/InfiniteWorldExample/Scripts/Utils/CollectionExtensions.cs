using System.Collections.Generic;

namespace PhysicsFloatingOrigin
{
    public static class CollectionExtensions
    {
        public static List<T> ToList<T>(this T[] array)
        {
            List<T> output = new List<T>();
            output.AddRange(array);
            output.Reverse();
            return output;
        }
    }
}
