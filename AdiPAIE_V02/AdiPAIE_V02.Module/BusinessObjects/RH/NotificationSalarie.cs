using DevExpress.Drawing;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.ConditionalAppearance;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;

using DevExpress.Persistent.Base;

using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using static AdiPAIE_V02.Module.Domain.DomainEnums;


namespace AdiPAIE_V02.Module.BusinessObjects.RH
{
    [DefaultClassOptions]
    [XafDisplayName("Notification salarié")]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("Action_SendMessage")]
  //  [NavigationItem("GRH - Espace salarié")]
    [Appearance("Notif_Urgent_Style", TargetItems = "*",
        Criteria = "Priorite = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NotificationPriorite,Urgent#",
        FontColor = "Red", FontStyle = DXFontStyle.Bold)]
    [Appearance("Notif_Important_Style", TargetItems = "*",
        Criteria = "Priorite = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NotificationPriorite,Important#",
        FontColor = "DarkOrange")]
    [Appearance("Notif_Archivee_Style", TargetItems = "*",
        Criteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NotificationStatut,Archivee#",
        FontColor = "Gray", FontStyle = DXFontStyle.Italic)]
    public class NotificationSalarie : BaseObject
    {
        public NotificationSalarie(Session session) : base(session) { }

        public override void AfterConstruction()
        {
            base.AfterConstruction();
            DateEnvoi = DateTime.Now;
            Statut = NotificationStatut.NonLue;
            Priorite = NotificationPriorite.Info;
            try { EnvoyePar = SecuritySystem.CurrentUserName; } catch { }
        }

        [Association("Salarie-Notifications")]
        [RuleRequiredField]
        [XafDisplayName("Salarié destinataire")]
        public Salarie Salarie
        {
            get => salarie;
            set => SetPropertyValue(nameof(Salarie), ref salarie, value);
        }
        Salarie salarie;

        string titre;
        [RuleRequiredField, Size(200), XafDisplayName("Titre")]
        public string Titre
        {
            get => titre;
            set => SetPropertyValue(nameof(Titre), ref titre, value?.Trim());
        }

        string corps;
        [Size(4096), XafDisplayName("Message")]
        public string Corps
        {
            get => corps;
            set => SetPropertyValue(nameof(Corps), ref corps, value?.Trim());
        }

        NotificationPriorite? priorite;
        [XafDisplayName("Priorité")]
        public NotificationPriorite? Priorite
        {
            get => priorite;
            set => SetPropertyValue(nameof(Priorite), ref priorite, value);
        }

        string categorie;
        [Size(80), XafDisplayName("Catégorie")]
        public string Categorie
        {
            get => categorie;
            set => SetPropertyValue(nameof(Categorie), ref categorie, value?.Trim());
        }

        string lienContextuel;
        [Size(200), XafDisplayName("Lien contextuel"), Browsable(false)]
        public string LienContextuel
        {
            get => lienContextuel;
            set => SetPropertyValue(nameof(LienContextuel), ref lienContextuel, value);
        }

        NotificationStatut? statut;
        [XafDisplayName("Statut")]
        public NotificationStatut? Statut
        {
            get => statut;
            set => SetPropertyValue(nameof(Statut), ref statut, value);
        }

        DateTime? dateLecture;
        [ModelDefault("AllowEdit", "False"), XafDisplayName("Date de lecture")]
        public DateTime? DateLecture
        {
            get => dateLecture;
            set => SetPropertyValue(nameof(DateLecture), ref dateLecture, value);
        }

        DateTime dateEnvoi;
        [ModelDefault("AllowEdit", "False"), XafDisplayName("Date d'envoi")]
        public DateTime DateEnvoi
        {
            get => dateEnvoi;
            set => SetPropertyValue(nameof(DateEnvoi), ref dateEnvoi, value);
        }

        string envoyePar;
        [Size(50), ModelDefault("AllowEdit", "False"), XafDisplayName("Envoyé par")]
        public string EnvoyePar
        {
            get => envoyePar;
            set => SetPropertyValue(nameof(EnvoyePar), ref envoyePar, value);
        }

        bool emailEnvoye;
        [ModelDefault("AllowEdit", "False"), XafDisplayName("Email envoyé")]
        public bool EmailEnvoye
        {
            get => emailEnvoye;
            set => SetPropertyValue(nameof(EmailEnvoye), ref emailEnvoye, value);
        }

        [NonPersistent]
        public string DisplayName => $"{Salarie?.LastName} – {Titre}";

        [NonPersistent, XafDisplayName("Non lue")]
        public bool EstNonLue => Statut == NotificationStatut.NonLue;

        [Action(Caption = "Marquer comme lue", ImageName = "Action_Approve", AutoCommit = true,
            TargetObjectsCriteria = "Statut = ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NotificationStatut,NonLue#")]
        public void MarquerCommeLue()
        {
            Statut = NotificationStatut.Lue;
            DateLecture = DateTime.Now;
        }

        [Action(Caption = "Archiver", ImageName = "BO_FileAttachment", AutoCommit = true,
            TargetObjectsCriteria = "Statut <> ##Enum#AdiPAIE_V02.Module.Domain.DomainEnums+NotificationStatut,Archivee#")]
        public void Archiver()
        {
            if (Statut == NotificationStatut.NonLue) DateLecture = DateTime.Now;
            Statut = NotificationStatut.Archivee;
        }


    }
}
