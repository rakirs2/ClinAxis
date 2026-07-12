using System.Text;

namespace Scrapers.Utilities;

public static class PersonUuid
{
    public static Guid Compute(string? name, string? affiliation)
    {
        var input = $"{name?.Trim().ToUpperInvariant()}|{affiliation?.Trim().ToUpperInvariant()}";
        var text = Encoding.UTF8.GetBytes(input);
        var hash = new byte[16];

        for (int i = 0; i < text.Length; i++)
        {
            hash[i % 16] ^= text[i];
            hash[(i + 3) % 16] ^= (byte)((text[i] + i) & 0xFF);
            hash[(i + 7) % 16] ^= (byte)((text[i] ^ (i << 1)) & 0xFF);
            hash[(i + 11) % 16] ^= (byte)((text[i] * 151) & 0xFF);
        }

        return new Guid(hash);
    }
}
