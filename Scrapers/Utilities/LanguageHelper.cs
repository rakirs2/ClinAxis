using System.Globalization;

namespace Scrapers.Utilities
{
    internal static class LanguageHelper
    {
        public static bool IsNonEnglish(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var nonLatinChars = 0;
            var totalLetters = 0;

            foreach (var c in text)
            {
                UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat != UnicodeCategory.LowercaseLetter && cat != UnicodeCategory.UppercaseLetter
                    && cat != UnicodeCategory.TitlecaseLetter && cat != UnicodeCategory.ModifierLetter
                    && cat != UnicodeCategory.OtherLetter)
                {
                    continue;
                }

                totalLetters++;
                var codePoint = char.ConvertToUtf32(text, text.IndexOf(c, StringComparison.Ordinal));
                if (IsCjk(codePoint) || IsCyrillic(codePoint) || IsArabic(codePoint)
                    || IsGreek(codePoint) || IsThai(codePoint) || IsHebrew(codePoint))
                {
                    nonLatinChars++;
                }
            }

            return totalLetters > 0 && (double)nonLatinChars / totalLetters >= 0.5;
        }

        private static bool IsCjk(int cp)
        {
            return (cp >= 0x4E00 && cp <= 0x9FFF) || (cp >= 0x3400 && cp <= 0x4DBF)
            || (cp >= 0x2E80 && cp <= 0x2EFF) || (cp >= 0xF900 && cp <= 0xFAFF)
            || (cp >= 0x2F800 && cp <= 0x2FA1F);
        }

        private static bool IsCyrillic(int cp)
        {
            return cp >= 0x0400 && cp <= 0x04FF;
        }

        private static bool IsArabic(int cp)
        {
            return cp >= 0x0600 && cp <= 0x06FF;
        }

        private static bool IsGreek(int cp)
        {
            return cp >= 0x0370 && cp <= 0x03FF;
        }

        private static bool IsThai(int cp)
        {
            return cp >= 0x0E00 && cp <= 0x0E7F;
        }

        private static bool IsHebrew(int cp)
        {
            return cp >= 0x0590 && cp <= 0x05FF;
        }
    }
}
