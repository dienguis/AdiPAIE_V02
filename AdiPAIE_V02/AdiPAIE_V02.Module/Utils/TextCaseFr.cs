using System;
using System.Collections.Generic;
using System.Globalization;

namespace AdiPAIE_V02.Module.Utils
{
    public static class TextCaseFr
    {
        static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        // Mots-outils a laisser en minuscule (sauf 1er mot)
        static readonly HashSet<string> LowerWords = new(StringComparer.OrdinalIgnoreCase) {
            "de","du","des","d","la","le","les","l","\u00e0","au","aux",
            "et","ou","par","pour","sur","dans","en","chez"
        };

        // Acronymes a preserver en MAJUSCULES
        static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase) {
          "IPRES","IR","IPM","CSS",
          "RG","RC","AT","AF",
          "TRIMF","CFCE","VRS","STC","HS",
          "CNSS","TVA","DGID","CDD","CDI"
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

            // Apostrophes : ASCII straight quote or Unicode right single quotation mark
            const char curlyApo = '\u2019';
            if (word.Contains(curlyApo) || word.Contains('\''))
            {
                char apo = word.Contains(curlyApo) ? curlyApo : '\'';
                var parts = word.Split(new[] { '\'', curlyApo }, 2);
                string head = parts[0];
                string rest = parts.Length > 1 ? parts[1] : null;

                string headOut = HandleHyphen(head, forceCap: isFirst);
                if (rest == null) return headOut;

                string restOut = HandleHyphen(rest, forceCap: true);
                return headOut + apo + restOut;
            }

            return HandleHyphen(word, forceCap: isFirst);
        }

        static string HandleHyphen(string token, bool forceCap)
        {
            var segs = token.Split('-', StringSplitOptions.None);
            for (int i = 0; i < segs.Length; i++)
                // i > 0 : chaque segment apres un trait d'union est capitalise
                // i == 0 : on respecte le forceCap du parent
                segs[i] = NormalizeSegment(segs[i], forceCap || i > 0);
            return string.Join("-", segs);
        }

        static string NormalizeSegment(string seg, bool forceCap)
        {
            if (string.IsNullOrEmpty(seg)) return seg;

            // 1. Acronymes explicites (ex: IPRES, IR, CSS)
            if (Acronyms.Contains(StripDots(seg)))
                return Acronymize(seg);

            var lower = seg.ToLower(Fr);

            // 2. Mots-outils en minuscule (sauf si forceCap = 1er mot de la phrase)
            if (!forceCap && LowerWords.Contains(lower))
                return lower;

            // 3. 1re lettre maj, reste min
            return char.ToUpper(lower[0], Fr) + (lower.Length > 1 ? lower[1..] : "");
        }

        static string StripDots(string s) => s?.Replace(".", "") ?? s;
        static string Acronymize(string s) => StripDots(s).ToUpper(Fr);
    }
}
