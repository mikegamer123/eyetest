using System.Text.RegularExpressions;

namespace EyeExamParser.Helpers
{
    public class JsonHelper
    {
        public static string Normalize(string? s)
        {
            return string.Join(
                " ",
                (s ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            );
        }

        public static string Normalize(IEnumerable<string> parts)
        {
            return Regex.Replace(
                string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p))),
                @"\s+",
                " "
            ).Trim();
        }
    }
}
