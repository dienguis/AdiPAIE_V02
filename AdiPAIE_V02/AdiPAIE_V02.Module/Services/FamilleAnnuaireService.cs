// =============================================================================
//  FamilleAnnuaireService.cs — V1.7 — Population de l'annuaire famille
//
//  Lit les Salariés depuis un OS persistant et matérialise des lignes
//  FamilleAnnuaire dans un OS non-persistant :
//    - 1 ligne par Salarié
//    - 1 ligne par Conjoint actuel (DateFinUnion = NULL ou future)
//    - 1 ligne par Enfant
//
//  Toutes les lignes partagent la même MatriculeSalarie (clé de regroupement).
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using DevExpress.ExpressApp;
using System.Collections.Generic;
using System.Linq;

namespace AdiPAIE_V02.Module.Services
{
    public static class FamilleAnnuaireService
    {
        /// <summary>
        /// Charge la liste hiérarchique de toutes les familles.
        /// </summary>
        /// <param name="persistentOs">OS pour lire Salariés/Conjoints/Enfants persistants.</param>
        /// <param name="nonPersistentOs">OS dans lequel matérialiser les lignes FamilleAnnuaire.</param>
        /// <param name="actifsUniquement">Si true, exclut les salariés inactifs.</param>
        /// <returns>Liste de FamilleAnnuaire prêts à afficher.</returns>
        public static List<FamilleAnnuaire> LoaderAnnuaire(
            IObjectSpace persistentOs,
            IObjectSpace nonPersistentOs,
            bool actifsUniquement = true)
        {
            var resultat = new List<FamilleAnnuaire>();

            var salariesQuery = persistentOs.GetObjectsQuery<Salarie>();
            var salaries = actifsUniquement
                ? salariesQuery.Where(s => s.IsActif).ToList()
                : salariesQuery.ToList();

            foreach (var salarie in salaries.OrderBy(s => s.Matricule))
            {
                var matricule = salarie.Matricule ?? "—";
                var titulaire = salarie.FullName ?? "—";

                // Ligne SALARIÉ
                var ligneSal = nonPersistentOs.CreateObject<FamilleAnnuaire>();
                ligneSal.MatriculeSalarie = matricule;
                ligneSal.SalarieTitulaire = titulaire;
                ligneSal.TypeMembre = TypeMembreFamille.Salarie;
                ligneSal.NomComplet = titulaire;
                ligneSal.DateNaissance = salarie.Birthday == default
                    ? (System.DateTime?)null : salarie.Birthday;
                ligneSal.Sexe = salarie.Sexe.ToString();
                ligneSal.StatutDetail = salarie.IsActif ? "Actif" : "Inactif";
                ligneSal.ACharge = false;
                resultat.Add(ligneSal);

                // Lignes CONJOINTS actuels
                var conjointsActifs = salarie.Conjoints
                    .Where(c => c.DateFinUnion == null || c.DateFinUnion > System.DateTime.Today)
                    .OrderBy(c => c.DateMariage);
                foreach (var conjoint in conjointsActifs)
                {
                    var ligneConj = nonPersistentOs.CreateObject<FamilleAnnuaire>();
                    ligneConj.MatriculeSalarie = matricule;
                    ligneConj.SalarieTitulaire = titulaire;
                    ligneConj.TypeMembre = TypeMembreFamille.Conjoint;
                    ligneConj.NomComplet = conjoint.NomComplet ?? "—";
                    ligneConj.DateNaissance = conjoint.DateNaissance;
                    ligneConj.Sexe = "—";
                    ligneConj.StatutDetail = conjoint.Statut.ToString();
                    ligneConj.ACharge = conjoint.ACharge;
                    resultat.Add(ligneConj);
                }

                // Lignes ENFANTS
                var enfants = salarie.Enfants.OrderBy(e => e.DateNaissance);
                foreach (var enfant in enfants)
                {
                    var ligneEnf = nonPersistentOs.CreateObject<FamilleAnnuaire>();
                    ligneEnf.MatriculeSalarie = matricule;
                    ligneEnf.SalarieTitulaire = titulaire;
                    ligneEnf.TypeMembre = TypeMembreFamille.Enfant;
                    ligneEnf.NomComplet = enfant.NomComplet ?? "—";
                    ligneEnf.DateNaissance = enfant.DateNaissance;
                    ligneEnf.Sexe = enfant.Sexe.ToString();
                    ligneEnf.StatutDetail = enfant.Situation.ToString();
                    ligneEnf.ACharge = enfant.ACharge;
                    resultat.Add(ligneEnf);
                }
            }

            return resultat;
        }
    }
}
