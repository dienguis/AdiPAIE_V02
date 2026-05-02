using AdiPAIE_V02.Module.Domain;
using AdiPAIE_V02.Module.Services;
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

                // Délègue la création au service centralisé (idempotent)
                var resultat = BulletinModeleService.CreerParDefaut(os, s, p);

                if (resultat == BulletinModeleService.ResultatCreation.Cree)
                {
                    os.CommitChanges();
                    Tracing.Tracer.LogText(
                        $"[MODELE_AUTO] Modèle créé pour Salarie={s.Matricule} ({s.FullName})");
                }
            }
            catch (Exception ex)
            {
                // Ne jamais bloquer l'UX — log uniquement
                Tracing.Tracer.LogError($"[MODELE_AUTO] Erreur création modèle : {ex.Message}");
            }
        }
    }
}
