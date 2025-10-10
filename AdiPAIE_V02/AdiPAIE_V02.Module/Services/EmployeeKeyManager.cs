using AdiPAIE_V02.Module.BusinessObjects;
using DevExpress.ExpressApp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdiPAIE_V02.Module.Services
{
    public static class EmployeeKeyManager
    {
        public static string EnsureKey(IObjectSpace os, Salarie salarie)
        {
            if (!string.IsNullOrEmpty(salarie.PayslipKeyEnc))
                return LocalSecretProtector.Unprotect(salarie.PayslipKeyEnc);

            var key = PayslipKeyService.NewKey();
            salarie.PayslipKeyEnc = LocalSecretProtector.Protect(key);
            salarie.PayslipKeyAssignedOn = DateTime.Now;
            os.CommitChanges();
            return key; // à notifier par canal séparé
        }
    }
}
