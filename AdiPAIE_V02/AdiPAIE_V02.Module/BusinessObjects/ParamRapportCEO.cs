// AdiPAIE_V02.Module/BusinessObjects/ParamRapportCEO.cs
// Objet non-persistant pour la popup du Rapport CEO.
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using System;
using System.ComponentModel;

namespace AdiPAIE_V02.Module.BusinessObjects
{
    [DomainComponent]
    [XafDefaultProperty(nameof(DisplayText))]
    public class ParamRapportCEO : INotifyPropertyChanged
    {
        private int _annee;
        private int _mois;
        private bool _genererPdf = true;
        private bool _genererExcel = true;
        private bool _envoyerEmail;

        [XafDisplayName("Année")]
        public int Annee
        {
            get => _annee;
            set { if (_annee != value) { _annee = value; OnChanged(nameof(Annee)); } }
        }

        [XafDisplayName("Mois (1-12)")]
        public int Mois
        {
            get => _mois;
            set { if (_mois != value) { _mois = value; OnChanged(nameof(Mois)); } }
        }

        [XafDisplayName("Générer PDF")]
        public bool GenererPdf
        {
            get => _genererPdf;
            set { if (_genererPdf != value) { _genererPdf = value; OnChanged(nameof(GenererPdf)); } }
        }

        [XafDisplayName("Générer Excel")]
        public bool GenererExcel
        {
            get => _genererExcel;
            set { if (_genererExcel != value) { _genererExcel = value; OnChanged(nameof(GenererExcel)); } }
        }

        [XafDisplayName("Envoyer par email")]
        public bool EnvoyerEmail
        {
            get => _envoyerEmail;
            set { if (_envoyerEmail != value) { _envoyerEmail = value; OnChanged(nameof(EnvoyerEmail)); } }
        }

        [Browsable(false)]
        public string DisplayText => $"Rapport CEO — {Mois:D2}/{Annee}";

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string prop)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
