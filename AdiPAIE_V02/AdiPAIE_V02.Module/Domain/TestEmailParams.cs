using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp;
using DevExpress.Persistent.Base;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.Domain
{
    [DomainComponent]
    [XafDisplayName("Envoyer un email de test")]
    public class TestEmailParams
    {
        [XafDisplayName("Destinataire (To)")]
        [ToolTip("Adresse qui recevra l'email de test")]
        public string To { get; set; }

        [XafDisplayName("Sujet")]
        [DefaultValue("Test SMTP AdiPAIE")]
        public string Subject { get; set; } = "Test SMTP AdiPAIE";

        [XafDisplayName("Message (HTML)")]
        [DefaultValue("<b>Ça fonctionne !</b>")]
        public string BodyHtml { get; set; } = "<b>Ça fonctionne !</b>";
    }
}
