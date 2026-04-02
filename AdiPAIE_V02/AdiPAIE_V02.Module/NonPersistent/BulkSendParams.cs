using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Editors;
using DevExpress.Persistent.Base;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
    [Appearance("HideOverride",
        Criteria = "DryRun = false",
        TargetItems = "OverrideEmail",
        Visibility = ViewItemVisibility.Hide)]
    public class BulkSendParams : NonPersistentBaseObject
    {
        // ── Champs internes — jamais affichés dans le popup ────
        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        [VisibleInLookupListView(false)]
        public Guid PeriodeOid { get; set; }

        [VisibleInDetailView(false)]
        [VisibleInListView(false)]
        [VisibleInLookupListView(false)]
        public string PeriodeCaption { get; set; }

        // ── Options visibles dans le popup ─────────────────────
        [XafDisplayName("Bulletins validés uniquement")]
        [ToolTip("Seuls les bulletins au statut Validé sont envoyés.")]
        public bool OnlyValidated { get; set; } = true;

        [XafDisplayName("Mode test (Dry-run)")]
        [ToolTip("Redirige tous les emails vers l'adresse de test. Décochez pour l'envoi réel.")]
        public bool DryRun { get; set; } = true;

        [XafDisplayName("Adresse e-mail de test")]
        [ToolTip("Tous les bulletins seront envoyés à cette adresse en mode test.")]
        public string OverrideEmail { get; set; } = string.Empty;
    }
}
