using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using DevExpress.Xpo;
using System;
using System.Collections.Generic;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    /// <summary>
    /// V1.8.7 - Service de détection d'anomalies d'intégrité.
    ///
    /// Chaque méthode Detecter* retourne une liste d'AnomalieIntegrite.
    /// Le controller ControleIntegriteController agrège tous les détecteurs
    /// et affiche le résultat dans une ListView.
    ///
    /// Chaque anomalie porte un CodeAction qui permet au controller de savoir
    /// quelle correction proposer (ex: "ReouvrirRevalider" -> réouvre puis
    /// revalide le bulletin fantôme).
    /// </summary>
    public static class IntegriteService
    {
        /// <summary>Lance tous les détecteurs et retourne la liste consolidée.</summary>
        public static List<AnomalieIntegrite> DetecterToutes(IObjectSpace os)
        {
            var toutes = new List<AnomalieIntegrite>();
            toutes.AddRange(DetecterBulletinsFantomesPrets(os));
            toutes.AddRange(DetecterDoublesPrelevementsPret(os));
            toutes.AddRange(DetecterBulletinLignesDupliquees(os));
            toutes.AddRange(DetecterCategoriesEstCadreIncoherentes(os));
            toutes.AddRange(DetecterTauxIRDivergent(os));
            return toutes;
        }

        // -----------------------------------------------------------------
        // 1) Bulletins fantômes : bulletin Cloturé mais échéances de prêt
        //    du même mois encore en statut Prevue (bug d'avant V1.8.6 où
        //    on pouvait passer Brouillon -> Cloture direct sans ValiderRemboursementsPrets).
        // -----------------------------------------------------------------
        public static List<AnomalieIntegrite> DetecterBulletinsFantomesPrets(IObjectSpace os)
        {
            var xpOs = os as DevExpress.ExpressApp.Xpo.XPObjectSpace;
            if (xpOs == null) return new List<AnomalieIntegrite>();
            var session = xpOs.Session;

            var result = new List<AnomalieIntegrite>();

            // Cherche tous les bulletins Cloturé avec des échéances Prevue
            // pour le salarié dans le mois du bulletin.
            var bulletinsClotures = new XPQuery<Bulletin>(session)
                .Where(b => b.Statut == BulletinStatut.Cloture)
                .ToList();

            foreach (var b in bulletinsClotures)
            {
                if (b.Salarie == null) continue;
                var debutMois = new DateTime(b.Annee, b.Mois, 1);
                var finMois = debutMois.AddMonths(1).AddDays(-1);

                var fantomes = new XPQuery<PretEcheance>(session)
                    .Where(ec => ec.Pret != null
                              && ec.Pret.Salarie == b.Salarie
                              && ec.DateEcheance >= debutMois
                              && ec.DateEcheance <= finMois
                              && ec.Statut == PretEcheanceStatut.Prevue)
                    .ToList();

                foreach (var ec in fantomes)
                {
                    result.Add(new AnomalieIntegrite
                    {
                        Categorie = "Prêt",
                        Severite = "Erreur",
                        TypeAnomalie = "Échéance fantôme",
                        Description = $"Bulletin clôturé mais échéance de {ec.MontantTotal:N0} FCFA reste en Prévue " +
                                       "(le prêt n'a pas été correctement marqué prélevé).",
                        ObjetConcerne = $"{b.Salarie?.FullName ?? "?"} - {b.Periode}",
                        ActionSuggere = "Réouvrir puis re-valider le bulletin (Statut Brouillon -> Valide)",
                        OidBulletin = b.Oid,
                        OidPretEcheance = ec.Oid,
                        OidSalarie = b.Salarie?.Oid,
                        MontantConcerne = ec.MontantTotal,
                        CodeAction = "ReouvrirRevalider",
                        Cle = $"FANTOME|{b.Oid}|{ec.Oid}"
                    });
                }
            }
            return result;
        }

        // -----------------------------------------------------------------
        // 2) Doubles prélèvements : deux BulletinLigne "RemboursementPret"
        //    pour le même Pret (via BulletinPreleveur d'échéance) sur des
        //    mois différents pour un montant identique - suspect.
        //    Version simple : compare les échéances Prelevee. Si N échéances
        //    Prelevee avec le même montant sur 2 bulletins consécutifs, alerte.
        // -----------------------------------------------------------------
        public static List<AnomalieIntegrite> DetecterDoublesPrelevementsPret(IObjectSpace os)
        {
            var xpOs = os as DevExpress.ExpressApp.Xpo.XPObjectSpace;
            if (xpOs == null) return new List<AnomalieIntegrite>();
            var session = xpOs.Session;

            var result = new List<AnomalieIntegrite>();

            // Version simple : détecter le cas où un bulletin a une ligne
            // "Retenue Prêt" > 0 ET où le salarié a ce même mois-là aussi
            // des échéances qui étaient en Prevue avant clôture (donc
            // reçues 2 fois potentiellement sur le bulletin suivant).
            //
            // Approche pragmatique : signaler les cas où le total des
            // échéances Prelevee d'un prêt dépasse le TotalAPrelever.
            var pretsSuspects = new XPQuery<Pret>(session)
                .Where(p => p.TotalPreleve > p.TotalAPrelever && p.TotalAPrelever > 0m)
                .ToList();

            foreach (var p in pretsSuspects)
            {
                var trop = p.TotalPreleve - p.TotalAPrelever;
                result.Add(new AnomalieIntegrite
                {
                    Categorie = "Prêt",
                    Severite = "Erreur",
                    TypeAnomalie = "Trop-prélevé sur prêt",
                    Description = $"Le total prélevé ({p.TotalPreleve:N0}) dépasse le total prévu ({p.TotalAPrelever:N0}). " +
                                   $"Excédent : {trop:N0} FCFA.",
                    ObjetConcerne = $"{p.Salarie?.FullName ?? "?"} - Prêt {p.Oid.ToString().Substring(0, 8)}",
                    ActionSuggere = "Créer une régul REGUL_PRET_GAIN sur le prochain bulletin",
                    OidPret = p.Oid,
                    OidSalarie = p.Salarie?.Oid,
                    MontantConcerne = trop,
                    CodeAction = "CreerRegulPretGain",
                    Cle = $"TROPPRELEVE|{p.Oid}"
                });
            }
            return result;
        }

        // -----------------------------------------------------------------
        // 3) BulletinLigne dupliquées : (Bulletin, Rubrique) doit être unique
        //    (contrainte V1.8). Les anciens doublons pré-V1.8 peuvent persister.
        // -----------------------------------------------------------------
        public static List<AnomalieIntegrite> DetecterBulletinLignesDupliquees(IObjectSpace os)
        {
            var xpOs = os as DevExpress.ExpressApp.Xpo.XPObjectSpace;
            if (xpOs == null) return new List<AnomalieIntegrite>();
            var session = xpOs.Session;

            var result = new List<AnomalieIntegrite>();

            var lignes = new XPQuery<BulletinLigne>(session)
                .Where(l => l.Bulletin != null && l.Rubrique != null)
                .ToList();

            var groupes = lignes
                .GroupBy(l => new { BulletinOid = l.Bulletin.Oid, RubriqueOid = l.Rubrique.Oid })
                .Where(g => g.Count() > 1);

            foreach (var g in groupes)
            {
                var doublons = g.ToList();
                var b = doublons.First().Bulletin;
                var r = doublons.First().Rubrique;

                result.Add(new AnomalieIntegrite
                {
                    Categorie = "Bulletin",
                    Severite = "Avertissement",
                    TypeAnomalie = "Ligne bulletin dupliquée",
                    Description = $"{doublons.Count} lignes avec la rubrique « {r.Code} » sur le même bulletin. " +
                                   $"Total : {doublons.Sum(x => x.Montant):N0} FCFA.",
                    ObjetConcerne = $"{b.Salarie?.FullName ?? "?"} - {b.Periode} - {r.Code}",
                    ActionSuggere = "Supprimer les doublons en gardant la ligne avec le montant le plus élevé",
                    OidBulletin = b.Oid,
                    OidBulletinLigne = doublons.OrderByDescending(x => x.Montant).First().Oid,
                    OidSalarie = b.Salarie?.Oid,
                    MontantConcerne = doublons.Sum(x => x.Montant),
                    CodeAction = "SupprimerLigneDoublons",
                    Cle = $"DOUBLON|{b.Oid}|{r.Oid}"
                });
            }
            return result;
        }

        // -----------------------------------------------------------------
        // 4) Categories.EstCadre incohérent (V1.8.1) : libellé commence par
        //    "Cadre" (hors "Non") mais EstCadre = false -> risque IPRES_RC manqué
        // -----------------------------------------------------------------
        public static List<AnomalieIntegrite> DetecterCategoriesEstCadreIncoherentes(IObjectSpace os)
        {
            var xpOs = os as DevExpress.ExpressApp.Xpo.XPObjectSpace;
            if (xpOs == null) return new List<AnomalieIntegrite>();
            var session = xpOs.Session;

            var result = new List<AnomalieIntegrite>();
            var rxNon = new System.Text.RegularExpressions.Regex(
                @"\bnon\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            var cats = new XPQuery<Categories>(session).ToList();
            foreach (var c in cats)
            {
                if (string.IsNullOrWhiteSpace(c.Intitule)) continue;
                var lib = c.Intitule.Trim();
                bool devraitEstCadre = lib.StartsWith("cadre", StringComparison.OrdinalIgnoreCase)
                                       && !rxNon.IsMatch(lib);

                if (devraitEstCadre && !c.EstCadre)
                {
                    result.Add(new AnomalieIntegrite
                    {
                        Categorie = "Référentiel",
                        Severite = "Avertissement",
                        TypeAnomalie = "Catégorie cadre non cochée",
                        Description = $"Le libellé « {lib} » suggère une catégorie cadre mais EstCadre = false. " +
                                       "Les salariés de cette catégorie ne recevront pas d'IPRES Régime Cadre.",
                        ObjetConcerne = c.Intitule,
                        ActionSuggere = "Cocher EstCadre sur la catégorie",
                        CodeAction = "CocherEstCadre",
                        Cle = $"CAT_ESTCADRE|{c.Oid}"
                    });
                }
            }
            return result;
        }

        // -----------------------------------------------------------------
        // 5) Taux IR (parts fiscales) figé et divergent de Salarie.NombrePartsFiscales
        //    (bug V1.8.2 : ligne IR avec Taux = 3 alors que fiche = 3,5)
        // -----------------------------------------------------------------
        public static List<AnomalieIntegrite> DetecterTauxIRDivergent(IObjectSpace os)
        {
            var xpOs = os as DevExpress.ExpressApp.Xpo.XPObjectSpace;
            if (xpOs == null) return new List<AnomalieIntegrite>();
            var session = xpOs.Session;

            var result = new List<AnomalieIntegrite>();

            var lignesIR = new XPQuery<BulletinLigne>(session)
                .Where(l => l.Bulletin != null
                         && l.Bulletin.Statut != BulletinStatut.Cloture
                         && l.Rubrique != null
                         && l.Rubrique.Canonique == RubriqueCanonique.IRPP
                         && l.Taux.HasValue)
                .ToList();

            foreach (var l in lignesIR)
            {
                var b = l.Bulletin;
                var s = b?.Salarie;
                if (s == null) continue;

                var partsFiche = s.NombrePartsFiscales;
                if (partsFiche <= 0) continue;

                if (Math.Abs(l.Taux.Value - partsFiche) > 0.001m)
                {
                    result.Add(new AnomalieIntegrite
                    {
                        Categorie = "Bulletin",
                        Severite = "Avertissement",
                        TypeAnomalie = "Parts fiscales divergentes",
                        Description = $"Ligne IR : {l.Taux.Value} parts, mais fiche salarié : {partsFiche} parts. " +
                                       "Le prochain « Recharger bulletin » alignera automatiquement (V1.8.2).",
                        ObjetConcerne = $"{s.FullName} - {b.Periode}",
                        ActionSuggere = "Ouvrir le bulletin et cliquer « Recharger bulletin »",
                        OidBulletin = b.Oid,
                        OidBulletinLigne = l.Oid,
                        OidSalarie = s.Oid,
                        CodeAction = "RechargerBulletin",
                        Cle = $"IRPARTS|{l.Oid}"
                    });
                }
            }
            return result;
        }
    }
}
