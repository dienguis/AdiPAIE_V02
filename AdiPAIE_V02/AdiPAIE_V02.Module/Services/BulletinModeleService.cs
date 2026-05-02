// AdiPAIE_V02.Module/Services/BulletinModeleService.cs
//
// Service centralisant la création par défaut d'un BulletinModele pour un salarié.
// Logique extraite de SalarieModeleOnSaveCloseController pour pouvoir être réutilisée :
//   - À chaque Save & Close d'une fiche salarié (controller existant)
//   - Par une action de création en masse sur la liste Salarié (CreerModelesEnMasseController)
using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Services
{
    public static class BulletinModeleService
    {
        public enum ResultatCreation
        {
            Cree,                  // Nouveau BulletinModele créé
            ExistantConserve,      // Un modèle actif existait déjà (rien fait)
            DesactiveParParametre  // ModeleAuto_CreerAuSave = false dans ParametresPaie
        }

        /// <summary>
        /// Crée un BulletinModele par défaut pour le salarié si aucun n'existe.
        /// Idempotent : ne fait rien si un modèle actif existe déjà.
        /// </summary>
        public static ResultatCreation CreerParDefaut(
            IObjectSpace os, Salarie salarie, ParametresPaie parametres)
        {
            if (os == null) throw new ArgumentNullException(nameof(os));
            if (salarie == null) throw new ArgumentNullException(nameof(salarie));

            // Respect du paramètre global d'activation
            if (!(parametres?.ModeleAuto_CreerAuSave ?? true))
                return ResultatCreation.DesactiveParParametre;

            // Idempotent : ne rien faire si un modèle actif existe déjà
            bool existe = os.GetObjectsQuery<BulletinModele>()
                            .Any(m => m.Salarie == salarie && m.Actif);
            if (existe)
                return ResultatCreation.ExistantConserve;

            var modele = os.CreateObject<BulletinModele>();
            modele.Salarie = salarie;
            modele.Actif = true;

            // ── Valeurs du salarié avec fallback ParametresPaie ───────────
            decimal salaireBase = Math.Max(salarie.SalaireBase, 0m);
            decimal logement = CoalescePositive(salarie.IndemniteLogement, null, 0m);

            decimal sursalaire = CoalescePositive(
                salarie.Sursalaire,
                parametres?.ModeleAuto_Defaut_Sursalaire,
                0m);

            decimal primeTransport = CoalescePositive(
                salarie.PrimeTransport,
                parametres?.ModeleAuto_Defaut_PrimeTransport,
                0m);

            decimal avantageVehicule = CoalescePositive(
                salarie.AvantageVehicule,
                parametres?.ModeleAuto_Defaut_AvantageVehicule,
                0m);

            // ── Lignes standard (ordre depuis Rubrique.OrdreAffichage ou auto) ─
            int cursor = 0;

            // Salaire de base — toujours inclus
            AddLigne(os, modele, RubriqueCanonique.SalaireDeBase, "SB",
                ref cursor,
                baseDefaut: salaireBase,
                montantDefaut: null,            // calculé dynamiquement par le moteur
                inclure: true);

            // Sursalaire — si > 0
            if (sursalaire > 0m)
                AddLigne(os, modele, RubriqueCanonique.Sursalaire, "SURSAL",
                    ref cursor,
                    baseDefaut: sursalaire,
                    montantDefaut: sursalaire,
                    inclure: true);

            // Indemnité logement — si > 0
            if (logement > 0m)
                AddLigne(os, modele, RubriqueCanonique.IndemniteLogement, "LOGT",
                    ref cursor,
                    baseDefaut: logement,
                    montantDefaut: null,        // prorata base30 calculé dynamiquement
                    inclure: true);

            // Prime de transport — si > 0 et pas de véhicule
            if (primeTransport > 0m && !salarie.PossedeVehicule)
                AddLigne(os, modele, RubriqueCanonique.PrimeTransport, "TRANS",
                    ref cursor,
                    baseDefaut: primeTransport,
                    montantDefaut: primeTransport,
                    inclure: true);

            // Avantage en nature véhicule — si > 0 et possède véhicule
            if (avantageVehicule > 0m && salarie.PossedeVehicule)
                AddLigne(os, modele, RubriqueCanonique.AvantageNatureVehicule, "AV_NAT_VEH",
                    ref cursor,
                    baseDefaut: avantageVehicule,
                    montantDefaut: avantageVehicule,
                    inclure: true);

            return ResultatCreation.Cree;
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private static void AddLigne(
            IObjectSpace os,
            BulletinModele modele,
            RubriqueCanonique canonique,
            string fallbackCode,
            ref int cursor,
            decimal? baseDefaut,
            decimal? montantDefaut,
            bool inclure)
        {
            // Cherche la rubrique par canonique, sinon par code
            var rubrique = os.GetObjectsQuery<Rubrique>()
                             .FirstOrDefault(r => r.Actif && r.Canonique == canonique)
                          ?? os.GetObjectsQuery<Rubrique>()
                             .FirstOrDefault(r => r.Actif && r.Code == fallbackCode);

            if (rubrique == null) return; // rubrique non configurée → on saute

            // Ordre : depuis la rubrique si défini, sinon curseur auto
            int ordre = (rubrique.OrdreAffichage.HasValue && rubrique.OrdreAffichage.Value > 0)
                ? rubrique.OrdreAffichage.Value
                : (cursor += 10);

            var ligne = os.CreateObject<BulletinModeleLigne>();
            ligne.Modele = modele;
            ligne.Rubrique = rubrique;
            ligne.Ordre = ordre;
            ligne.InclureParDefaut = inclure;
            ligne.BaseDefaut = baseDefaut;
            ligne.MontantDefaut = montantDefaut;
            ligne.TauxDefaut = null;
        }

        /// <summary>
        /// Retourne la valeur primaire si > 0, sinon le fallback nullable,
        /// sinon fallbackIfNull. Utilisé pour prioriser la valeur salarié
        /// sur le paramètre global.
        /// </summary>
        private static decimal CoalescePositive(
            decimal primary,
            decimal? fallbackNullable,
            decimal fallbackIfNull = 0m)
        {
            if (primary > 0m) return primary;
            var fb = fallbackNullable ?? fallbackIfNull;
            return fb > 0m ? fb : 0m;
        }
    }
}
