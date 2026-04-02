using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Calcule la distance routière entre deux villes
    /// via l'API Google Maps Distance Matrix.
    ///
    /// Prérequis : clé API Google Maps avec Distance Matrix activée
    /// configurée dans ParametresPaie.GoogleMapsApiKey.
    ///
    /// Usage :
    ///   var km = await GoogleMapsService.GetDistanceKmAsync(
    ///       "Dakar", "Thiès", apiKey);
    /// </summary>
    public static class GoogleMapsService
    {
        private const string BaseUrl =
            "https://maps.googleapis.com/maps/api/distancematrix/json";

        /// <summary>
        /// Retourne la distance en km entre deux localités sénégalaises.
        /// Retourne null en cas d'erreur ou de résultat introuvable.
        /// </summary>
        public static async Task<int?> GetDistanceKmAsync(
            string origine, string destination, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "Clé API Google Maps non configurée. "
                    + "Renseignez-la dans Paramètres de paie → Google Maps API Key.");

            if (string.IsNullOrWhiteSpace(origine)
             || string.IsNullOrWhiteSpace(destination))
                return null;

            // Ajouter ", Sénégal" si pas déjà présent pour préciser le pays
            var orig = AppendSenegal(origine);
            var dest = AppendSenegal(destination);

            var url = $"{BaseUrl}?origins={Uri.EscapeDataString(orig)}"
                    + $"&destinations={Uri.EscapeDataString(dest)}"
                    + $"&mode=driving"
                    + $"&language=fr"
                    + $"&key={apiKey}";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await http.GetStringAsync(url);

            return ParseDistance(response);
        }

        // ── Parsing de la réponse JSON ────────────────────────────────
        private static int? ParseDistance(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Vérifier le statut global
                if (root.TryGetProperty("status", out var status)
                 && status.GetString() != "OK")
                    return null;

                // Extraire rows[0].elements[0]
                var rows = root.GetProperty("rows");
                if (rows.GetArrayLength() == 0) return null;

                var elements = rows[0].GetProperty("elements");
                if (elements.GetArrayLength() == 0) return null;

                var element = elements[0];

                // Vérifier le statut de l'élément
                if (element.TryGetProperty("status", out var elemStatus)
                 && elemStatus.GetString() != "OK")
                    return null;

                // Extraire la distance en mètres → convertir en km
                var distanceMeters = element
                    .GetProperty("distance")
                    .GetProperty("value")
                    .GetInt64();

                return (int)Math.Round(distanceMeters / 1000.0);
            }
            catch
            {
                return null;
            }
        }

        private static string AppendSenegal(string ville)
        {
            var v = ville.Trim();
            if (v.EndsWith("Sénégal", StringComparison.OrdinalIgnoreCase)
             || v.EndsWith("Senegal", StringComparison.OrdinalIgnoreCase))
                return v;
            return v + ", Sénégal";
        }


    }
}
