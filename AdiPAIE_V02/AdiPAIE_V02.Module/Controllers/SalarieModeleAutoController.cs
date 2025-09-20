using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.SystemModule;
using DevExpress.ExpressApp.Utils; // Tracing
using DevExpress.Persistent.Base;   // InformationType
using System;

public class SalarieModeleAutoController : ObjectViewController<DetailView, Salarie>
{
    protected override void OnActivated()
    {
        base.OnActivated();
        var mods = Frame.GetController<ModificationsController>();
        if (mods != null)
        {
            mods.SaveAndCloseAction.Executed -= SaveAndClose_Executed;
            mods.SaveAndCloseAction.Executed += SaveAndClose_Executed;
        }
    }

    private void SaveAndClose_Executed(object sender, ActionBaseEventArgs e)
    {
        try
        {
            var os = ObjectSpace;
            var s = View?.CurrentObject as Salarie;
            if (s == null) return;

            // Ta logique existante de création “idempotente”
            var modele = EnsureBulletinModeleForSalarie(os, s, out bool created, out int nbLignes);

            // 🔔 LOG DISCRET (toast) + trace
            if (created)
            {
                ShowToast($"Modèle de bulletin créé pour {s.FullName} ({nbLignes} lignes).");
                Tracing.Tracer.LogText($"[MODELE_AUTO] Created for Salarie={s.Oid}, Lines={nbLignes}");
            }
            else if (modele != null)
            {
                ShowToast($"Modèle déjà présent pour {s.FullName} (aucune création).", InformationType.Info);
                Tracing.Tracer.LogText($"[MODELE_AUTO] Skipped (already exists) for Salarie={s.Oid}");
            }
            else
            {
                ShowToast($"Impossible de déterminer/créer le modèle pour {s.FullName}.", InformationType.Warning);
                Tracing.Tracer.LogText($"[MODELE_AUTO] No model returned for Salarie={s.Oid}");
            }
        }
        catch (Exception ex)
        {
            // En cas d’exception, on notifie sans bloquer l’UX
            ShowToast("Erreur lors de la création du modèle. Voir les logs.", InformationType.Error, 5000);
            Tracing.Tracer.LogError(ex);
        }
    }

    // === Helpers ============================================================
    private void ShowToast(string message,
                           InformationType type = InformationType.Success,
                           int msecs = 3000)
    {
        Application?.ShowViewStrategy?.ShowMessage(
            message, type, msecs, InformationPosition.Bottom);
    }

    /// <summary>
    /// Ta méthode d’origine : crée si absent, ne duplique pas (idempotent).
    /// Retourne le modèle et indique si on l’a créé + le nombre de lignes.
    /// </summary>
    private BulletinModele EnsureBulletinModeleForSalarie(
        IObjectSpace os, Salarie s, out bool created, out int nbLignes)
    {

        created = false; nbLignes = 0;

        // 1) existe déjà ?
        var existing = os.GetObjectsQuery<BulletinModele>()
                         .FirstOrDefault(m => m.Salarie == s && m.Actif);
        if (existing != null)
        {
            nbLignes = existing.Lignes?.Count ?? 0;
            return existing;
        }

        // 2) créer + alimenter
        var m = os.CreateObject<BulletinModele>();
        m.Salarie = s;
        m.Actif = true;

        // …ajoute ici EXACTEMENT ta logique de lignes (SB, SUR, TRSP, AVNVeh…)
        // nbLignes = m.Lignes.Count;

        created = true;
        return m;
    }
}
