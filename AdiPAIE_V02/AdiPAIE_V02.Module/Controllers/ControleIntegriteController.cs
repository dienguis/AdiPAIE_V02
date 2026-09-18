// =============================================================================
//  ControleIntegriteController.cs - V1.8.7 - Menu Contrôle d'intégrité
//
//  Pattern XAF canonique pour un NonPersistentObjectSpace :
//    1. À l'activation, on récupère le NonPersistentObjectSpace sous-jacent
//    2. On s'abonne à ObjectsGetting qui est appelé quand XAF a besoin des
//       données pour peupler la vue
//    3. Dans le handler, on lance IntegriteService.DetecterToutes et on
//       clone les anomalies dans le NonPersistentObjectSpace
//
//  Action Actualiser : force le rescan (Reset du CollectionSource)
//  Action Corriger  : applique la correction sur les anomalies sélectionnées
// =============================================================================

using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.NonPersistent;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.Controllers
{
    public class ControleIntegriteController : ObjectViewController<ListView, AnomalieIntegrite>
    {
        private readonly SimpleAction _actualiser;
        private readonly SimpleAction _corriger;
        private NonPersistentObjectSpace _nonPersistentOs;

        public ControleIntegriteController()
        {
            _actualiser = new SimpleAction(this, "Integrite_Actualiser", PredefinedCategory.Edit)
            {
                Caption = "Actualiser",
                ImageName = "Action_Refresh",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Relance tous les détecteurs d'intégrité",
                SelectionDependencyType = SelectionDependencyType.Independent
            };
            _actualiser.Execute += Actualiser_Execute;

            _corriger = new SimpleAction(this, "Integrite_Corriger", PredefinedCategory.Edit)
            {
                Caption = "Corriger",
                ImageName = "Action_Debug_Start",
                PaintStyle = ActionItemPaintStyle.Caption,
                ToolTip = "Applique la correction suggérée à l'anomalie sélectionnée",
                SelectionDependencyType = SelectionDependencyType.RequireMultipleObjects,
                ConfirmationMessage = "Appliquer la correction à la/aux anomalie(s) sélectionnée(s) ?"
            };
            _corriger.Execute += Corriger_Execute;
        }

        protected override void OnActivated()
        {
            base.OnActivated();
            _nonPersistentOs = GetNonPersistentObjectSpace(ObjectSpace);
            if (_nonPersistentOs != null)
                _nonPersistentOs.ObjectsGetting += NonPersistentOs_ObjectsGetting;
        }

        protected override void OnDeactivated()
        {
            if (_nonPersistentOs != null)
            {
                _nonPersistentOs.ObjectsGetting -= NonPersistentOs_ObjectsGetting;
                _nonPersistentOs = null;
            }
            base.OnDeactivated();
        }

        // --------------------------------------------------------------
        // Récupère le NonPersistentObjectSpace, qu'il soit direct ou
        // encapsulé dans une CompositeObjectSpace.
        // --------------------------------------------------------------
        private static NonPersistentObjectSpace GetNonPersistentObjectSpace(IObjectSpace os)
        {
            if (os is NonPersistentObjectSpace direct) return direct;
            if (os is CompositeObjectSpace composite)
            {
                return composite.AdditionalObjectSpaces
                    .OfType<NonPersistentObjectSpace>()
                    .FirstOrDefault();
            }
            return null;
        }

        // --------------------------------------------------------------
        // Handler XAF : lancer les détecteurs et retourner les anomalies
        // --------------------------------------------------------------
        private void NonPersistentOs_ObjectsGetting(object sender, ObjectsGettingEventArgs e)
        {
            if (e.ObjectType != typeof(AnomalieIntegrite)) return;

            using var osPersist = Application.CreateObjectSpace(typeof(Bulletin));
            var scans = IntegriteService.DetecterToutes(osPersist);

            var liste = new BindingList<AnomalieIntegrite>();
            foreach (var a in scans)
            {
                var copie = _nonPersistentOs.CreateObject<AnomalieIntegrite>();
                copie.Categorie = a.Categorie;
                copie.Severite = a.Severite;
                copie.TypeAnomalie = a.TypeAnomalie;
                copie.Description = a.Description;
                copie.ObjetConcerne = a.ObjetConcerne;
                copie.ActionSuggere = a.ActionSuggere;
                copie.OidBulletin = a.OidBulletin;
                copie.OidPret = a.OidPret;
                copie.OidSalarie = a.OidSalarie;
                copie.OidBulletinLigne = a.OidBulletinLigne;
                copie.OidPretEcheance = a.OidPretEcheance;
                copie.CodeAction = a.CodeAction;
                copie.MontantConcerne = a.MontantConcerne;
                copie.Cle = a.Cle;
                liste.Add(copie);
            }
            e.Objects = liste;
        }

        // --------------------------------------------------------------
        // Actions
        // --------------------------------------------------------------
        private void Actualiser_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            View.CollectionSource.ResetCollection();
            var count = View.CollectionSource.List.OfType<AnomalieIntegrite>().Count();
            Application.ShowViewStrategy?.ShowMessage(
                $"Contrôle d'intégrité terminé : {count} anomalie(s) détectée(s).",
                count > 0 ? InformationType.Warning : InformationType.Success,
                4000, InformationPosition.Top);
        }

        private void Corriger_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var anomalies = e.SelectedObjects?.OfType<AnomalieIntegrite>().ToList()
                          ?? new System.Collections.Generic.List<AnomalieIntegrite>();
            if (anomalies.Count == 0)
                throw new UserFriendlyException("Aucune anomalie sélectionnée.");

            int ok = 0, ko = 0;
            var erreurs = new System.Collections.Generic.List<string>();

            using var osFix = Application.CreateObjectSpace(typeof(Bulletin));
            var session = ((XPObjectSpace)osFix).Session;

            foreach (var a in anomalies)
            {
                try
                {
                    switch (a.CodeAction)
                    {
                        case "ReouvrirRevalider":
                            ReouvrirRevaliderBulletin(session, a);
                            break;
                        case "CocherEstCadre":
                            CocherEstCadre(session, a);
                            break;
                        case "RechargerBulletin":
                            RechargerBulletin(session, a);
                            break;
                        case "SupprimerLigneDoublons":
                            SupprimerDoublons(session, a);
                            break;
                        case "CreerRegulPretGain":
                            throw new UserFriendlyException(
                                "Correction manuelle : ouvrir un bulletin futur, ajouter une ligne " +
                                $"REGUL_PRET_GAIN pour {a.MontantConcerne:N0} FCFA.");
                        default:
                            throw new UserFriendlyException(
                                $"Type d'anomalie non pris en charge en correction auto : {a.CodeAction}");
                    }
                    ok++;
                }
                catch (Exception ex)
                {
                    ko++;
                    erreurs.Add($"{a.ObjetConcerne} : {ex.Message}");
                }
            }
            osFix.CommitChanges();

            // Ré-actualiser après correction
            View.CollectionSource.ResetCollection();

            var msg = ok > 0
                ? $"{ok} correction(s) appliquée(s)."
                : "Aucune correction appliquée.";
            if (ko > 0)
                msg += $"\n{ko} échec(s) :\n* " + string.Join("\n* ", erreurs.Take(10));

            Application.ShowViewStrategy?.ShowMessage(
                msg,
                ok > 0 ? InformationType.Success : InformationType.Warning,
                ko > 0 ? 8000 : 4000, InformationPosition.Top);
        }

        // --------------------------------------------------------------
        // Correctifs par type
        // --------------------------------------------------------------
        private void ReouvrirRevaliderBulletin(Session s, AnomalieIntegrite a)
        {
            if (!a.OidBulletin.HasValue)
                throw new UserFriendlyException("Bulletin manquant.");
            var b = s.GetObjectByKey<Bulletin>(a.OidBulletin.Value);
            if (b == null) throw new UserFriendlyException("Bulletin introuvable.");

            // Réouvrir : Cloture/Valide/etc. -> Brouillon (échéances Prelevee liées -> Prevue)
            if (b.Statut != BulletinStatut.Brouillon)
            {
                var echPreleveesLiees = new XPQuery<PretEcheance>(s)
                    .Where(ec => ec.BulletinPreleveur == b && ec.Statut == PretEcheanceStatut.Prelevee)
                    .ToList();
                foreach (var ec in echPreleveesLiees)
                {
                    ec.Statut = PretEcheanceStatut.Prevue;
                    ec.BulletinPreleveur = null;
                }
                b.Statut = BulletinStatut.Brouillon;
            }

            // Valider : Brouillon -> Valide + marque les échéances Prelevee
            b.Statut = BulletinStatut.Valide;
            b.ValiderRemboursementsPrets();
        }

        private void CocherEstCadre(Session s, AnomalieIntegrite a)
        {
            var lib = a.ObjetConcerne;
            var cat = new XPQuery<Categories>(s)
                .FirstOrDefault(c => c.Intitule == lib);
            if (cat == null) throw new UserFriendlyException("Catégorie introuvable.");
            cat.EstCadre = true;
        }

        private void RechargerBulletin(Session s, AnomalieIntegrite a)
        {
            if (!a.OidBulletin.HasValue)
                throw new UserFriendlyException("Bulletin manquant.");
            var b = s.GetObjectByKey<Bulletin>(a.OidBulletin.Value);
            if (b == null) throw new UserFriendlyException("Bulletin introuvable.");
            b.RecalculerDepuisParametrage();
        }

        private void SupprimerDoublons(Session s, AnomalieIntegrite a)
        {
            if (!a.OidBulletin.HasValue)
                throw new UserFriendlyException("Bulletin manquant.");
            var b = s.GetObjectByKey<Bulletin>(a.OidBulletin.Value);
            if (b == null) throw new UserFriendlyException("Bulletin introuvable.");

            var ligneMax = s.GetObjectByKey<BulletinLigne>(a.OidBulletinLigne ?? Guid.Empty);
            if (ligneMax == null || ligneMax.Rubrique == null)
                throw new UserFriendlyException("Ligne principale introuvable.");

            var doublons = new XPQuery<BulletinLigne>(s)
                .Where(l => l.Bulletin == b && l.Rubrique == ligneMax.Rubrique)
                .ToList();

            foreach (var l in doublons)
            {
                if (l.Oid != ligneMax.Oid)
                    l.Delete();
            }
        }
    }
}
