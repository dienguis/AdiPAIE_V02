using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Crée automatiquement un BulletinModele actif pour un salarié
    /// uniquement lors de "Save & Close" de la fiche salarié.
    /// </summary>
    public class SalarieModeleOnSaveCloseController : ObjectViewController<DetailView, Salarie>
    {
        private ModificationsController _mods;
        private Guid? _pendingSalarieOid;

        protected override void OnActivated()
        {
            base.OnActivated();

            _mods = Frame.GetController<ModificationsController>();
            if (_mods != null)
            {
                // Save simple ⇒ ne crée pas le modèle
                _mods.SaveAction.Executing += SaveAction_Executing_ClearPending;

                // Save & Close ⇒ marquer l’intention
                _mods.SaveAndCloseAction.Executing += SaveAndCloseAction_Executing_Mark;

                // Cancel ⇒ nettoyer le flag
                _mods.CancelAction.Executing += CancelAction_Executing_ClearPending;
            }

            // Création après COMMIT effectif
            View.ObjectSpace.Committed += ObjectSpace_Committed_AfterSaveAndClose;

            // Filet de sécurité si la vue se ferme autrement
            View.Closed += View_Closed_Cleanup;
        }

        protected override void OnDeactivated()
        {
            if (_mods != null)
            {
                _mods.SaveAction.Executing -= SaveAction_Executing_ClearPending;
                _mods.SaveAndCloseAction.Executing -= SaveAndCloseAction_Executing_Mark;
                _mods.CancelAction.Executing -= CancelAction_Executing_ClearPending;
            }

            if (View?.ObjectSpace != null)
                View.ObjectSpace.Committed -= ObjectSpace_Committed_AfterSaveAndClose;

            View.Closed -= View_Closed_Cleanup;
            _pendingSalarieOid = null;

            base.OnDeactivated();
        }

        private void SaveAction_Executing_ClearPending(object sender, EventArgs e)
        {
            // Un "Save" simple ne doit pas déclencher la création auto
            _pendingSalarieOid = null;
        }

        private void CancelAction_Executing_ClearPending(object sender, EventArgs e)
        {
            // Annulation : on nettoie aussi
            _pendingSalarieOid = null;
        }

        private void SaveAndCloseAction_Executing_Mark(object sender, EventArgs e)
        {
            if (View?.CurrentObject is Salarie s && View.IsRoot)
                _pendingSalarieOid = s.Oid;
        }

        private void View_Closed_Cleanup(object sender, EventArgs e)
        {
            // Si la vue se ferme sans commit (erreur, etc.), ne rien déclencher
            _pendingSalarieOid = null;
        }

        private void ObjectSpace_Committed_AfterSaveAndClose(object sender, EventArgs e)
        {
            if (!_pendingSalarieOid.HasValue) return;

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Salarie));
                var s = os.GetObjectByKey<Salarie>(_pendingSalarieOid.Value);
                _pendingSalarieOid = null; // consommer le flag

                if (s == null) return;

                var p = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();

                // Paramètre global pour autoriser / bloquer la création auto
                var autoriserCreation = (p?.ModeleAuto_CreerAuSave ?? true);
                if (!autoriserCreation) return;

                // Ne rien faire si un modèle actif existe déjà
                var existe = os.GetObjectsQuery<BulletinModele>().Any(m => m.Salarie == s && m.Actif);
                if (existe) return;

                CreerModeleParDefaut(os, s, p);
                os.CommitChanges();
            }
            catch
            {
                // Optionnel : log interne si tu as une infra de logging
            }
        }

        // ===== Helpers "valeurs par défaut" & ordre =====

        private static decimal CoalescePositive(decimal primary, decimal? fallbackNullable, decimal fallbackIfNull = 0m)
        {
            // Si la valeur primaire (issue du salarié) est > 0 -> on la prend
            if (primary > 0m) return primary;

            // Sinon on tente le paramètre (qui peut être decimal?)
            var fb = fallbackNullable ?? fallbackIfNull;
            return fb > 0m ? fb : 0m;
        }

        private static int NextOrdreFromRubrique(Rubrique rub, ref int cursor, int step = 10)
        {
            // Si la rubrique a un ordre d’affichage défini et > 0, on le reprend
            if (rub?.OrdreAffichage.HasValue == true && rub.OrdreAffichage.Value > 0)
                return rub.OrdreAffichage.Value;

            // Sinon, on incrémente un curseur local
            cursor += step;
            return cursor;
        }

        // ===== Construction du modèle par défaut =====

        private static void CreerModeleParDefaut(IObjectSpace os, Salarie s, ParametresPaie p)
        {
            var modele = os.CreateObject<BulletinModele>();
            modele.Salarie = s;
            modele.Actif = true;
            // Si tu as un champ Libelle :
            // modele.Libelle = $"Modèle par défaut - {s.FullName}";

            // 1) Valeurs issues du salarié (décimaux non-nullables) avec fallback ParametresPaie (decimal?)
            var salaireBase = Math.Max(s.SalaireBase, 0m);

            var sursalaire = CoalescePositive(
                s.Sursalaire,
                p?.ModeleAuto_Defaut_Sursalaire /* decimal? */,
                0m
            );

            var logement = CoalescePositive(
                s.IndemniteLogement,
                p?.ModeleAuto_Defaut_Sursalaire /* decimal? */,
                0m
            );

            var primeTrans = CoalescePositive(
                s.PrimeTransport,
                p?.ModeleAuto_Defaut_PrimeTransport /* decimal? */,
                0m
            );

            var avVeh = CoalescePositive(
                s.AvantageVehicule,
                p?.ModeleAuto_Defaut_AvantageVehicule /* decimal? */,
                0m
            );

            // 2) Ajout des lignes en respectant l’ordre de la rubrique si disponible
            int ordreCursor = 100;

            AddLigne(os, modele, RubriqueCanonique.SalaireDeBase, ref ordreCursor,
                baseDefaut: (salaireBase > 0m ? salaireBase : 0m),
                montantDefaut: null,
                inclureParDefaut: true);

            if (sursalaire > 0m)
                AddLigne(os, modele, RubriqueCanonique.Sursalaire, ref ordreCursor,
                    baseDefaut: sursalaire, montantDefaut: sursalaire, inclureParDefaut: true);


            if (logement > 0m)
                AddLigne(os, modele, RubriqueCanonique.IndemniteLogement, ref ordreCursor,
                    baseDefaut: logement, montantDefaut: logement, inclureParDefaut: true);

            if (primeTrans > 0m)
                AddLigne(os, modele, RubriqueCanonique.PrimeTransport, ref ordreCursor,
                    baseDefaut: primeTrans, montantDefaut: primeTrans, inclureParDefaut: true);

            if (avVeh > 0m)
                AddLigne(os, modele, RubriqueCanonique.AvantageNatureVehicule, ref ordreCursor,
                    baseDefaut: avVeh, montantDefaut: avVeh, inclureParDefaut: true);
        }

        private static void AddLigne(
            IObjectSpace os,
            BulletinModele modele,
            RubriqueCanonique canonique,
            ref int ordreCursor,
            decimal? baseDefaut,
            decimal? montantDefaut,
            bool inclureParDefaut)
        {
            var rub = os.GetObjectsQuery<Rubrique>()
                        .FirstOrDefault(r => r.Canonique == canonique && r.Actif);
            if (rub == null) return;

            var l = os.CreateObject<BulletinModeleLigne>();
            l.Modele = modele;
            l.Rubrique = rub;

            // Ordre : priorité à OrdreAffichage de la rubrique
            l.Ordre = NextOrdreFromRubrique(rub, ref ordreCursor);

            l.InclureParDefaut = inclureParDefaut;

            // On n’affecte BaseDefaut / MontantDefaut que si > 0
            if (baseDefaut.HasValue && baseDefaut.Value > 0m)
                l.BaseDefaut = baseDefaut.Value;

            // Si la rubrique est de type "montant" (pas %), poser MontantDefaut directement
            if (montantDefaut.HasValue && montantDefaut.Value > 0m)
                l.MontantDefaut = montantDefaut.Value;

            // Pour une rubrique en %, tu pourrais renseigner l.TauxDefaut si besoin
            // l.TauxDefaut = 5m; // exemple
        }
    }
}
