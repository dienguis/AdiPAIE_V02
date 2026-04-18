using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Editors;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    /// <summary>
    /// Contrôleur pour la saisie des heures supplémentaires sur un bulletin.
    /// - Visible uniquement en DetailView du bulletin.
    /// - Le bouton "Ajouter HS" est masqué si ActiverHeuresSupplementaires = false.
    /// - La collection HeuresSupplementaires est masquée si le module est désactivé.
    /// </summary>
    public class BulletinHeuresSupController : ObjectViewController<DetailView, Bulletin>
    {
        private readonly PopupWindowShowAction addHSAction;

        public BulletinHeuresSupController()
        {
            addHSAction = new PopupWindowShowAction(this, "Bulletin_AjouterHS", PredefinedCategory.Edit)
            {
                Caption = "Ajouter HS",
                ImageName = "Action_Grant",
                ToolTip = "Ajouter des heures supplémentaires au bulletin."
            };
            addHSAction.CustomizePopupWindowParams += AddHS_CustomizePopup;
            addHSAction.Execute += AddHS_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            UpdateHSVisibility();
        }

        private void UpdateHSVisibility()
        {
            var prm = ParametresPaie.TryGet(ObjectSpace);
            var actif = prm?.ActiverHeuresSupplementaires ?? false;

            // Masquer le bouton si HS désactivées
            addHSAction.Active["HSModule"] = actif;
        }

        private void AddHS_CustomizePopup(object sender, CustomizePopupWindowParamsEventArgs e)
        {
            var bulletin = View.CurrentObject as Bulletin;
            if (bulletin == null) return;

            var osHS = Application.CreateObjectSpace(typeof(HeureSupplementaireParam));
            var param = osHS.CreateObject<HeureSupplementaireParam>();

            // Pré-remplir depuis les paramètres de paie
            var prm = ParametresPaie.TryGet(ObjectSpace);
            param.TauxMajoration = prm?.GetTauxHS(TypeHeureSupplementaire.JourOuvrable)
                                   ?? HeureSupplementaire.GetTauxLegalDefaut(TypeHeureSupplementaire.JourOuvrable);

            var dv = Application.CreateDetailView(osHS, param);
            dv.ViewEditMode = ViewEditMode.Edit;
            e.View = dv;
            e.DialogController.SaveOnAccept = false;
        }

        private void AddHS_Execute(object sender, PopupWindowShowActionExecuteEventArgs e)
        {
            var param = e.PopupWindowViewCurrentObject as HeureSupplementaireParam;
            if (param == null) return;

            var bulletin = View.CurrentObject as Bulletin;
            if (bulletin == null) return;

            if (bulletin.Statut != BulletinStatut.Brouillon)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Le bulletin doit être en Brouillon pour ajouter des heures supplémentaires.",
                    InformationType.Warning, 4000, InformationPosition.Top);
                return;
            }

            if (param.NombreHeures <= 0)
            {
                Application.ShowViewStrategy.ShowMessage(
                    "Le nombre d'heures doit être supérieur à 0.",
                    InformationType.Warning, 3000, InformationPosition.Top);
                return;
            }

            // Créer la ligne HS dans l'ObjectSpace du bulletin
            var hs = ObjectSpace.CreateObject<HeureSupplementaire>();
            hs.Bulletin = bulletin;
            hs.TypeHS = param.TypeHS;
            hs.NombreHeures = param.NombreHeures;

            // Taux depuis paramétrage
            var prm = ParametresPaie.TryGet(ObjectSpace);
            hs.TauxMajoration = prm != null
                ? prm.GetTauxHS(param.TypeHS)
                : HeureSupplementaire.GetTauxLegalDefaut(param.TypeHS);

            hs.CalculerTauxHoraireBase();
            hs.RecalculerMontants();

            ObjectSpace.CommitChanges();

            // Recalculer le bulletin pour intégrer les HS dans les gains
            bulletin.RecalculerSurGrilleExistante();
            ObjectSpace.CommitChanges();

            View.ObjectSpace.Refresh();

            Application.ShowViewStrategy.ShowMessage(
                $"{param.NombreHeures:N2}h ({param.TypeHS}) ajoutées — {hs.MontantTotal:N0} FCFA.",
                InformationType.Success, 4000, InformationPosition.Bottom);
        }
    }

    // ── Paramètre non-persistant pour le popup ──
    [DevExpress.ExpressApp.DC.DomainComponent]
    [DevExpress.ExpressApp.DC.XafDefaultProperty(nameof(DisplayText))]
    public class HeureSupplementaireParam : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

        private TypeHeureSupplementaire typeHS = TypeHeureSupplementaire.JourOuvrable;
        [DevExpress.ExpressApp.DC.XafDisplayName("Type")]
        public TypeHeureSupplementaire TypeHS
        {
            get => typeHS;
            set { typeHS = value; OnChanged(nameof(TypeHS)); UpdateTaux(); }
        }

        private decimal nombreHeures;
        [DevExpress.ExpressApp.DC.XafDisplayName("Nombre d'heures")]
        [DevExpress.ExpressApp.Model.ModelDefault("DisplayFormat", "N2")]
        [DevExpress.ExpressApp.Model.ModelDefault("EditMask", "N2")]
        public decimal NombreHeures
        {
            get => nombreHeures;
            set { nombreHeures = value; OnChanged(nameof(NombreHeures)); }
        }

        private decimal tauxMajoration;
        [DevExpress.ExpressApp.DC.XafDisplayName("Taux majoration (%)")]
        [DevExpress.ExpressApp.Model.ModelDefault("DisplayFormat", "N2")]
        [DevExpress.ExpressApp.Model.ModelDefault("AllowEdit", "False")]
        public decimal TauxMajoration
        {
            get => tauxMajoration;
            set { tauxMajoration = value; OnChanged(nameof(TauxMajoration)); }
        }

        private void UpdateTaux()
        {
            TauxMajoration = HeureSupplementaire.GetTauxLegalDefaut(TypeHS);
        }

        public string DisplayText => $"{TypeHS} — {NombreHeures:N2}h";
    }
}
