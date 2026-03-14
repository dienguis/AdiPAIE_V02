using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Xpo;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DefaultClassOptions]
    [DefaultProperty(nameof(DisplayName))]
    [ImageName("BO_FileAttachment")]
    public class DossierPieceJointe : BaseObject
    {
        public DossierPieceJointe(Session session) : base(session) { }

        DossierDocument dossierDocument;
        [Association("DossierDocument-PiecesJointes")]
        public DossierDocument DossierDocument
        {
            get => dossierDocument;
            set => SetPropertyValue(nameof(DossierDocument), ref dossierDocument, value);
        }

        string titre;
        [Size(150)]
        public string Titre
        {
            get => titre;
            set => SetPropertyValue(nameof(Titre), ref titre, value);
        }

        FileData fichier;
        [Aggregated]
        [ExpandObjectMembers(ExpandObjectMembers.Never)]
        public FileData Fichier
        {
            get => fichier;
            set => SetPropertyValue(nameof(Fichier), ref fichier, value);
        }


        [NonPersistent]
        public string DisplayName => string.IsNullOrWhiteSpace(Titre)
            ? Fichier?.FileName
            : Titre;
    }
}