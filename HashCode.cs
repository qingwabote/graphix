namespace Graphix
{
    public class HashCode
    {
        // https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
        public static int Combine(int a, int b)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                return hash;
            }
        }

        public static int Combine(int a, int b, int c)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + a;
                hash = hash * 31 + b;
                hash = hash * 31 + c;
                return hash;
            }
        }
    }
}