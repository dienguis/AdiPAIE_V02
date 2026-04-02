using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// Import des villes sénégalaises depuis l'API GeoNames.
    ///
    /// Prérequis : compte gratuit sur https://www.geonames.org/login
    ///   → activer le webservice dans "manage account"
    ///   → renseigner le username dans Paramètres de paie → GeoNames Username
    ///
    /// URL appelée :
    ///   http://api.geonames.org/searchJSON
    ///     ?country=SN&featureClass=P&maxRows=1000&username=YOUR_USERNAME
    /// </summary>
    public static class GeoNamesService
    {
        private const string BaseUrl = "http://api.geonames.org/searchJSON";

        public class VilleImportee
        {
            public string Nom { get; set; }
            public string Region { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public int Population { get; set; }
        }

        /// <summary>
        /// Récupère toutes les localités du Sénégal (featureClass=P).
        /// Retourne une liste triée par population décroissante.
        /// </summary>
        public static async Task<List<VilleImportee>> GetVillesSenegalAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException(
                    "GeoNames Username non configuré. "
                    + "Renseignez-le dans Paramètres de paie → Intégrations → GeoNames Username.");

            var url = $"{BaseUrl}?country=SN&featureClass=P"
                    + $"&maxRows=1000&orderby=population&style=SHORT"
                    + $"&username={Uri.EscapeDataString(username)}";

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var json = await http.GetStringAsync(url);

            return ParseResponse(json);
        }

        private static List<VilleImportee> ParseResponse(string json)
        {
            var result = new List<VilleImportee>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Vérifier s'il y a un message d'erreur GeoNames
                if (root.TryGetProperty("status", out var status))
                {
                    var msg = status.TryGetProperty("message", out var m)
                        ? m.GetString() : "Erreur GeoNames inconnue";
                    throw new InvalidOperationException($"GeoNames erreur : {msg}");
                }

                if (!root.TryGetProperty("geonames", out var geonames))
                    return result;

                foreach (var item in geonames.EnumerateArray())
                {
                    var nom = item.TryGetProperty("name", out var n)
                        ? n.GetString()?.Trim() : null;
                    if (string.IsNullOrWhiteSpace(nom)) continue;

                    var region = item.TryGetProperty("adminName1", out var r)
                        ? r.GetString()?.Trim() : "";

                    double lat = 0, lng = 0;
                    if (item.TryGetProperty("lat", out var latProp))
                        double.TryParse(latProp.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out lat);
                    if (item.TryGetProperty("lng", out var lngProp))
                        double.TryParse(lngProp.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out lng);

                    int pop = 0;
                    if (item.TryGetProperty("population", out var popProp))
                        pop = popProp.GetInt32();

                    result.Add(new VilleImportee
                    {
                        Nom = nom,
                        Region = region ?? "",
                        Latitude = lat,
                        Longitude = lng,
                        Population = pop
                    });
                }
            }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Erreur parsing GeoNames : {ex.Message}");
            }

            return result;
        }
    }
}
