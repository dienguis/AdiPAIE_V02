using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AdiPAIE_V02.Module.Utils
{
    public static class TextCaseFr
    {
        static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        // Mots-outils à laisser en minuscule (sauf 1er mot)
        static readonly HashSet<string> LowerWords = new(StringComparer.OrdinalIgnoreCase) {
            "de","du","des","d","la","le","les","l","à","au","aux",
            "et","ou","par","pour","sur","dans","en","chez"
        };

        // Acronymes à préserver (ajoute-en si besoin)
        static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase) {
          "IPRES","IR","IPM","CSS"
        };

        public static string ToTitleCaseFrPreserveAcronyms(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            input = input.Trim();

            var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
                words[i] = TitleWord(words[i], isFirst: i == 0);

            return string.Join(" ", words);
        }

        static string TitleWord(string word, bool isFirst)
        {
            if (string.IsNullOrEmpty(word)) return word;

            // Apostrophes (’ ou ')
            if (word.Contains('’') || word.Contains('\''))
            {
                char apo = word.Contains('’') ? '’' : '\'';
                var parts = word.Split(new[] { '\'', '’' }, 2);
                string head = parts[0];
                string rest = parts.Length > 1 ? parts[1] : null;

                string headOut = HandleHyphen(head, forceCap: isFirst);
                if (rest == null) return headOut;

                string restOut = HandleHyphen(rest, forceCap: true); // après apostrophe, on force la majuscule
                return headOut + apo + restOut;
            }

            return HandleHyphen(word, forceCap: isFirst);
        }

        static string HandleHyphen(string token, bool forceCap)
        {
            var segs = token.Split('-', StringSplitOptions.None);
            for (int i = 0; i < segs.Length; i++)
                segs[i] = NormalizeSegment(segs[i], forceCap || i == 0);
            return string.Join("-", segs);
        }

        static string NormalizeSegment(string seg, bool forceCap)
        {
            if (string.IsNullOrEmpty(seg)) return seg;

            // Acronymes (ex: CNSS, IR, TVA)
            if (Acronyms.Contains(StripDots(seg)))
                return Acronymize(seg);

            // Si segment déjà TOUT en MAJ (≥2 lettres) → conserver
            var letters = new string(seg.Where(char.IsLetter).ToArray());
            if (letters.Length >= 2 && letters.All(c => char.IsUpper(c)))
                return seg.ToUpper(Fr);

            var lower = seg.ToLower(Fr);

            // Mots-outils en minuscule (sauf si forceCap)
            if (!forceCap && LowerWords.Contains(lower))
                return lower;

            // 1re lettre maj, reste min
            return char.ToUpper(lower[0], Fr) + (lower.Length > 1 ? lower[1..] : "");
        }

        static string StripDots(string s) => s?.Replace(".", "") ?? s;
        static string Acronymize(string s) => StripDots(s).ToUpper(Fr);
    }
}
