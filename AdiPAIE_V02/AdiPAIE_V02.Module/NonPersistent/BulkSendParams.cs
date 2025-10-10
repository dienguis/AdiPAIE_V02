// NonPersistent/BulkSendParams.cs
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;

namespace AdiPAIE_V02.Module.NonPersistent
{
    [DomainComponent]
   // [DefaultClassOptions]
    public class BulkSendParams : IObjectSpaceLink
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [NonPersistent] public IObjectSpace ObjectSpace { get; set; }

        public Guid PeriodeOid { get; set; }
        public string PeriodeCaption { get; set; }

        public bool OnlyValidated { get; set; } = true;
        public bool DryRun { get; set; } = true;
        public string OverrideEmail { get; set; } = "tests@exemple.com";
    }
}
