using System.Text;

namespace GatewayServer.Utils;

public static class PayloadHelper
{
    public static string NormalizeHexToSpaced(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var hexOnly = new StringBuilder();

        foreach (var ch in input)
        {
            if (Uri.IsHexDigit(ch))
            {
                hexOnly.Append(char.ToUpperInvariant(ch));
            }
        }

        if (hexOnly.Length == 0 || hexOnly.Length % 2 != 0)
            return string.Empty;

        var result = new StringBuilder();

        for (int i = 0; i < hexOnly.Length; i += 2)
        {
            if (result.Length > 0)
                result.Append(' ');

            result.Append(hexOnly[i]);
            result.Append(hexOnly[i + 1]);
        }

        return result.ToString();
    }
}