namespace Veeling.Models;

public static class HashUtil
{
    public static ulong ComputeFnv1a64(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offsetBasis;

        var bytes = System.Text.Encoding.UTF8.GetBytes(input);

        foreach (byte b in bytes)
        {
            hash ^= b;
            hash *= prime;
        }

        return hash;
    }

    public static string ToBase36(ulong value)
    {
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";

        if (value == 0) return "0";

        Span<char> buffer = stackalloc char[13]; // enough for base36 ulong
        int i = buffer.Length;

        while (value > 0)
        {
            buffer[--i] = chars[(int)(value % 36)];
            value /= 36;
        }

        return new string(buffer[i..]);
    }
}
