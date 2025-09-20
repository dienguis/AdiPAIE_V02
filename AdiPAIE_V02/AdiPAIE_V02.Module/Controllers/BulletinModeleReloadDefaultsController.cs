using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    public class BulletinModeleReloadDefaultsController
        : ObjectViewController<DetailView, BulletinModele>
    {
        public BulletinModeleReloadDefaultsController()
        {
            var act = new SimpleAction(this, "ReloadModeleDefaults", PredefinedCategory.Edit)
            {
                Caption = "Recharger lignes standard",
                ImageName = "Action_Refresh",
                ToolTip = "Met à jour les lignes SB/LOGT/SUR/TRSP/AVNV selon la fiche salarié, sans supprimer vos lignes perso.",
                PaintStyle = ActionItemPaintStyle.CaptionAndImage
            };
            act.Execute += Act_Execute;
        }

        private void Act_Execute(object sender, SimpleActionExecuteEventArgs e)
        {
            var os = ObjectSpace;
            var m = View?.CurrentObject as BulletinModele;
            if (m == null || m.Salarie == null)
            {
                Application.ShowViewStrategy.ShowMessage("Aucun salarié rattaché au modèle.", InformationType.Warning, 3000, InformationPosition.Bottom);
                return;
            }

            // 1) Cherche les rubriques standard (canonique d’abord, sinon code)
            Rubrique rSB = FindRubrique(RubriqueCanonique.SalaireDeBase, "SB");
            Rubrique rLOGT = FindRubrique(RubriqueCanonique.IndemniteLogement, "LOGT");
            Rubrique rSUR = FindRubrique(RubriqueCanonique.Sursalaire, "SURSAL");
            Rubrique rTRSP = FindRubrique(RubriqueCanonique.PrimeTransport, "TRANS");
            Rubrique rAVNV = FindRubrique(RubriqueCanonique.AvantageNatureVehicule, "AV_NAT_VEH");

    
        int touched = 0;

            // 2) Salaire de base — base par défaut, montant laissé au moteur
            if (rSB != null)
            {
                var sbBase = m.Salarie?.SalaireBase ?? 0m;
                Upsert(m, rSB,
                    baseDefaut: sbBase,
                    tauxDefaut: null,
                    montantDefaut: null,
                    inclure: sbBase> 0m,
                    reference: "[AUTO] Salaire de base");
                touched++;
            }

            // 3) Indemnité logement — base, montant calculé par moteur
            if (rLOGT != null)
            {
                var imLog = m.Salarie?.IndemniteLogement ?? 0m;
                Upsert(m, rLOGT,
                    baseDefaut: imLog,
                    tauxDefaut: null,
                    montantDefaut: null,
                    inclure: imLog > 0m,
                    reference: "[AUTO] Logement");
                touched++;
            }

            // 4) Sursalaire — montant fixe
            if (rSUR != null)
            {
                var val = m.Salarie?.Sursalaire ?? 0m;
                
                Upsert(m, rSUR,
                    baseDefaut: 0m,
                    tauxDefaut: null,
                    montantDefaut: val,
                    inclure: val > 0m,
                    reference: "[AUTO] Sursalaire");
                touched++;
            }

            // 5) Prime transport — montant fixe
            if (rTRSP != null)
            {
                var val = m.Salarie?.PrimeTransport ?? 0m;
                Upsert(m, rTRSP,
                    baseDefaut: 0m,
                    tauxDefaut: null,
                    montantDefaut: val,
                    inclure: val > 0m,
                    reference: "[AUTO] Prime transport");
                touched++;
            }

            // 6) Avantage en nature véhicule — montant fixe (fallback 20 000 si null)
            if (rAVNV != null)
            {
                var defVeh = m.Salarie?.AvantageVehicule ?? 20000m;
                Upsert(m, rAVNV,
                    baseDefaut: 0m,
                    tauxDefaut: null,
                    montantDefaut: defVeh,
                    inclure: defVeh > 0m,
                    reference: "[AUTO] Avantage véhicule");
                touched++;
            }

            os.CommitChanges();
            Application.ShowViewStrategy.ShowMessage(
                $"Lignes standard rechargées ({touched} familles traitées). Vos autres lignes sont conservées.",
                InformationType.Success, 3000, InformationPosition.Bottom
            );
        }

        // ===== Helpers =======================================================

        private Rubrique FindRubrique(RubriqueCanonique? role, string fallbackCode)
        {
            var q = ObjectSpace.GetObjectsQuery<Rubrique>().Where(r => r.Actif);
            if (role.HasValue)
            {
                var byRole = q.FirstOrDefault(r => r.Canonique == role.Value);
                if (byRole != null) return byRole;
            }
            return q.FirstOrDefault(r => r.Code == fallbackCode);
        }

        private void Upsert(BulletinModele m, Rubrique rub,
                            decimal baseDefaut, decimal? tauxDefaut, decimal? montantDefaut,
                            bool inclure, string reference)
        {
            // cherche une ligne existante du même Rubrique (ou même Canonique)
            var line = m.Lignes.FirstOrDefault(l => l.Rubrique != null && l.Rubrique.Oid == rub.Oid)
                    ?? m.Lignes.FirstOrDefault(l => l.Rubrique != null && l.Rubrique.Canonique == rub.Canonique);

            if (line == null)
            {
                line = ObjectSpace.CreateObject<BulletinModeleLigne>();
                line.Modele = m;
                line.Rubrique = rub;
                // ordre par défaut : reprend l’ordre d’affichage de la rubrique ou “à la fin”
                line.Ordre = rub.OrdreAffichage ?? ((m.Lignes.Select(x => x.Ordre).DefaultIfEmpty(0).Max()) + 10);
            }

            line.ReferenceDefaut = reference;
            line.InclureParDefaut = inclure;

            line.BaseDefaut = baseDefaut;
            line.TauxDefaut = tauxDefaut;
            line.MontantDefaut = montantDefaut;
        }
    }
}
