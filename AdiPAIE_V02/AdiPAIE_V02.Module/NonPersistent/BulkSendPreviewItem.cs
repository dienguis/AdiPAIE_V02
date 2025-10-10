// NonPersistent/BulkSendPreviewItem.cs
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;     // DomainComponent
using DevExpress.Persistent.Base;   // DefaultClassOptions
using DevExpress.Persistent.Validation; // Key
using DevExpress.Xpo;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
   // [DefaultClassOptions]
    public class BulkSendPreviewItem : IObjectSpaceLink
    {
        // Clé requise pour les Domain Components
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Liaison à l’ObjectSpace (utile pour SetModified)
        [NonPersistent] public IObjectSpace ObjectSpace { get; set; }

        // --- Tes champs ---
        public Guid BulletinOid { get; set; }
        public bool Selected { get; set; } = true;

        public string Periode { get; set; }
        public string Matricule { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }

        public bool HasArchive { get; set; }
        public string FileName { get; set; }
        public string Note { get; set; }
    }
}
