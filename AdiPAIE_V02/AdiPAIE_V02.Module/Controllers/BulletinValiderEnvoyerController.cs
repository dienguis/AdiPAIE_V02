using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Services;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Templates;
using DevExpress.Persistent.Base;
using System;
using System.IO;
using System.Net.Mime;                       // MediaTypeNames.Application.Pdf
using static AdiPAIE_V02.Module.Domain.DomainEnums;
using Attachment = System.Net.Mail.Attachment; // évite l’ambiguïté avec Graph

public sealed class BulletinValiderEnvoyerController
    : ObjectViewController<DetailView, Bulletin>
{
    private readonly SimpleAction _validerEtEnvoyer;

    public BulletinValiderEnvoyerController()
    {
        _validerEtEnvoyer = new SimpleAction(this, "ValiderEtEnvoyer", PredefinedCategory.RecordEdit)
        {
            Caption = "Valider et envoyer",
            ImageName = "BO_Mail",
            PaintStyle = ActionItemPaintStyle.CaptionAndImage,
            ConfirmationMessage = "Valider ce bulletin et l'envoyer par email ?"
        };
        _validerEtEnvoyer.Execute += OnExecute;
    }

    private void OnExecute(object sender, SimpleActionExecuteEventArgs e)
    {
        var b = View.CurrentObject as Bulletin;
        if (b == null) return;

        if (string.IsNullOrWhiteSpace(b.Salarie?.Email))
            throw new UserFriendlyException("Le salarié n'a pas d'adresse e-mail.");

        // 1) Valider (si encore brouillon)
        if (b.Statut == BulletinStatut.Brouillon)
            b.Statut = BulletinStatut.Valide;

        ObjectSpace.CommitChanges(); // lance les recalculs et sauvegarde

        // 2) PDF du rapport créé dans ReportsV2 (XRpt_Bulletin)
        var pdfBytes = BulletinPdfService.BuildPdfByBulletinOid(ObjectSpace, Frame, b.Oid);
        var fileName = $"Bulletin_{b.Periode}_{b.Salarie?.Matricule}.pdf";

        // 3) Envoi email
        // APRÈS (OK)
        var p = ParametresPaie.TryGet(ObjectSpace)
                ?? throw new UserFriendlyException("Paramètres de paie introuvables.");


        var senderSvc = p.CreateEmailSender();

        using var ms = new MemoryStream(pdfBytes);
        using var att = new Attachment(ms, fileName, MediaTypeNames.Application.Pdf);

        var subject = $"Bulletin de paie – {b.Periode}";
        var bodyHtml = $@"
<p>Bonjour {b.Salarie?.FullName},</p>
<p>Veuillez trouver ci-joint votre bulletin de paie pour <b>{b.Periode}</b>.</p>
<p>Cordialement,<br/>{p.MailFromDisplayName}</p>";

        senderSvc.Send(b.Salarie.Email, subject, bodyHtml, att);

        // 4) Statut final
        b.Statut = BulletinStatut.Envoye;
        ObjectSpace.CommitChanges();

        Application.ShowViewStrategy.ShowMessage(
            "Bulletin validé et envoyé.", InformationType.Success, 3000, InformationPosition.Top);
    }
}
