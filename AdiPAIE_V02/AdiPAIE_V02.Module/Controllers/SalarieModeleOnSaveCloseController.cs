using AdiPAIE_V02.Module.Domain;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.Persistent.Base;
using DevExpress.ExpressApp.Utils;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    /// <summary>
    /// Crée automatiquement un BulletinModele actif pour un salarié
    /// UNIQUEMENT lors de "Save &amp; Close" de la fiche salarié.
    ///
    /// Corrections v2 :
    ///   - Bug logement : utilisait le fallback Sursalaire au lieu de IndemniteLogement
    ///   - catch vide remplacé par Tracer.LogError
    ///   - Lignes correctement créées avec bons ordres depuis PaieConsts
    ///   - SalarieModeleAutoController supprimé (doublon incomplet)
    /// </summary>
    public class SalarieModeleOnSaveCloseController
        : ObjectViewController<DetailView, Salarie>
    {
        private ModificationsController _mods;
        private Guid? _pendingSalarieOid;

        protected override void OnActivated()
        {
            base.OnActivated();

            _mods = Frame.GetController<ModificationsController>();
            if (_mods != null)
            {
                _mods.SaveAction.Executing += SaveAction_Executing_ClearPending;
                _mods.SaveAndCloseAction.Executing += SaveAndCloseAction_Executing_Mark;
                _mods.CancelAction.Executing += CancelAction_Executing_ClearPending;
            }

            View.ObjectSpace.Committed += ObjectSpace_Committed_AfterSaveAndClose;
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

        // ── Handlers ──────────────────────────────────────────────────────
        private void SaveAction_Executing_ClearPending(object sender, EventArgs e)
            => _pendingSalarieOid = null; // Save simple → pas de création

        private void CancelAction_Executing_ClearPending(object sender, EventArgs e)
            => _pendingSalarieOid = null;

        private void SaveAndCloseAction_Executing_Mark(object sender, EventArgs e)
        {
            if (View?.CurrentObject is Salarie s && View.IsRoot)
                _pendingSalarieOid = s.Oid;
        }

        private void View_Closed_Cleanup(object sender, EventArgs e)
            => _pendingSalarieOid = null;

        private void ObjectSpace_Committed_AfterSaveAndClose(object sender, EventArgs e)
        {
            if (!_pendingSalarieOid.HasValue) return;

            var oid = _pendingSalarieOid.Value;
            _pendingSalarieOid = null; // consommer le flag avant tout traitement

            try
            {
                using var os = Application.CreateObjectSpace(typeof(Salarie));

                var s = os.GetObjectByKey<Salarie>(oid);
                if (s == null) return;

                var p = os.GetObjectsQuery<ParametresPaie>().FirstOrDefault();

                // Respect du paramètre global d'activation
                if (!(p?.ModeleAuto_CreerAuSave ?? true)) return;

                // Idempotent : ne rien faire si un modèle actif existe déjà
                bool existe = os.GetObjectsQuery<BulletinModele>()
                                .Any(m => m.Salarie == s && m.Actif);
                if (existe) return;

                CreerModeleParDefaut(os, s, p);
                os.CommitChanges();

                Tracing.Tracer.LogText(
                    $"[MODELE_AUTO] Modèle créé pour Salarie={s.Matricule} ({s.FullName})");
            }
            catch (Exception ex)
            {
                // Ne jamais bloquer l'UX — log uniquement
                Tracing.Tracer.LogError($"[MODELE_AUTO] Erreur création modèle : {ex.Message}");
            }
        }

        // ── Construction du modèle ────────────────────────────────────────
        private static void CreerModeleParDefaut(
            IObjectSpace os, Salarie s, ParametresPaie p)
        {
            var modele = os.CreateObject<BulletinModele>();
            modele.Salarie = s;
            modele.Actif = true;

            // ── Valeurs du salarié avec fallback ParametresPaie ───────────
            decimal salaireBase = Math.Max(s.SalaireBase, 0m);

            decimal logement = CoalescePositive(s.IndemniteLogement, null, 0m);
            // ⚠ BUG CORRIGÉ : logement utilisait p?.ModeleAuto_Defaut_Sursalaire par erreur

            decimal sursalaire = CoalescePositive(
                s.Sursalaire,
                p?.ModeleAuto_Defaut_Sursalaire,
                0m);

            decimal primeTransport = CoalescePositive(
                s.PrimeTransport,
                p?.ModeleAuto_Defaut_PrimeTransport,
                0m);

            decimal avantageVehicule = CoalescePositive(
                s.AvantageVehicule,
                p?.ModeleAuto_Defaut_AvantageVehicule,
                0m);

            // ── Lignes standard (ordre depuis PaieConsts) ─────────────────
            int cursor = 0;

            // Salaire de base — toujours inclus
            AddLigne(os, modele, RubriqueCanonique.SalaireDeBase, "SB",
                ref cursor,
                baseDefaut: salaireBase,
                montantDefaut: null,  // calculé dynamiquement par le moteur
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
                    montantDefaut: null, // calculé dynamiquement (prorata base30)
                    inclure: true);

            // Prime de transport — si > 0 et pas de véhicule
            if (primeTransport > 0m && !s.PossedeVehicule)
                AddLigne(os, modele, RubriqueCanonique.PrimeTransport, "TRANS",
                    ref cursor,
                    baseDefaut: primeTransport,
                    montantDefaut: primeTransport,
                    inclure: true);

            // Avantage en nature véhicule — si possède véhicule
            if (avantageVehicule > 0m && s.PossedeVehicule)
                AddLigne(os, modele, RubriqueCanonique.AvantageNatureVehicule, "AV_NAT_VEH",
                    ref cursor,
                    baseDefaut: avantageVehicule,
                    montantDefaut: avantageVehicule,
                    inclure: true);
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
