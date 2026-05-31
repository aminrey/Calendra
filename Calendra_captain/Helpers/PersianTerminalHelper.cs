using System.Text.RegularExpressions;

namespace Calendra.Helpers
{
    public static class PersianTerminalHelper
    {
        // این متد کلمات انگلیسی بین جملات فارسی را بین دو پرانتز قرار می‌دهد
        public static string FormatEnglishWordsInPersianText(string input)
        {
            // فقط کلمات انگلیسی که بین حروف فارسی یا فاصله قرار دارند را هدف قرار می‌دهد
            return Regex.Replace(input, @"(?<=[\p{IsArabic}\s])([A-Za-z0-9_\-\.]+)(?=[\p{IsArabic}\s])", match => $"({match.Value})");
        }
    }
}
