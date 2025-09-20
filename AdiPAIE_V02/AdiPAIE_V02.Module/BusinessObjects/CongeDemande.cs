using AdiPAIE_V02.Module.BusinessObjects;
using AdiPAIE_V02.Module.Domain;
using DevExpress.DataAccess.Native.Sql.QueryBuilder;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Persistent.BaseImpl;
using DevExpress.Persistent.Validation;
using DevExpress.Xpo;
using System;
using System.ComponentModel;
using System.Linq;
using static AdiPAIE_V02.Module.Domain.DomainEnums;

[DefaultClassOptions, XafDisplayName("Demande de congé")]
[DefaultProperty(nameof(DisplayName))]
[RuleCriteria("Conge_DateFin_GTE_DateDebut", DefaultContexts.Save, "DateFin >= DateDebut",
    CustomMessageTemplate = "La date de fin doit être ≥ à la date de début.")]
public class CongeDemande : BaseObject
{
    public CongeDemande(Session s) : base(s) { }

    [RuleRequiredField, Association("Salarie-Conges")]
    public Salarie Salarie { get => sal; set => SetPropertyValue(nameof(Salarie), ref sal, value); }
    Salarie sal;

    [RuleRequiredField]
    public CongeType Type { get => type; set => SetPropertyValue(nameof(Type), ref type, value); }
    CongeType type;

    [RuleRequiredField]
    public DateTime DateDebut { get => d1; set => SetPropertyValue(nameof(DateDebut), ref d1, value); }
    DateTime d1 = DateTime.Today;


    //[RuleRequiredField]
    //[RuleCriteria("Conge_DateFin>=DateDebut", DefaultContexts.Save, "DateFin >= DateDebut",
    //    CustomMessageTemplate = "La date de fin doit être ≥ à la date de début.")]

    [RuleRequiredField]
    public DateTime DateFin
    {
        get => d2;
        set => SetPropertyValue(nameof(DateFin), ref d2, value.Date);
    }
    private DateTime d2 = DateTime.Today;

    public CongeStatut Statut { get => statut; set => SetPropertyValue(nameof(Statut), ref statut, value); }
    CongeStatut statut =  CongeStatut.Brouillon;

    [ModelDefault("DisplayFormat", "N2"), ModelDefault("EditMask", "N2")]
    [DbType("decimal(18,2)")]
    public decimal DureeJours { get => duree; set => SetPropertyValue(nameof(DureeJours), ref duree, value); }
    decimal duree;

    [Size(240)]
    public string Motif { get => motif; set => SetPropertyValue(nameof(Motif), ref motif, value?.Trim()); }
    string motif;

    [NonPersistent, XafDisplayName("Période")]
    public string DisplayName => $"{Salarie?.FullName} : {DateDebut:dd/MM} → {DateFin:dd/MM} ({DureeJours:n2} j)";

    public override void AfterConstruction()
    {
        base.AfterConstruction();
        RecalculerDuree();
    }

    protected override void OnSaving()
    {
        base.OnSaving();
        if (!IsDeleted) RecalculerDuree();
    }

    public void RecalculerDuree()
    {
        DureeJours = CalculerJoursDemande(DateDebut, DateFin, Type?.CompteEnJoursOuvrables ?? true);
    }

    private decimal CalculerJoursDemande(DateTime dStart, DateTime dEnd, bool ouvrables)
    {
        if (dEnd < dStart) return 0m;
        var dates = Enumerable.Range(0, (dEnd - dStart).Days + 1)
                              .Select(i => dStart.AddDays(i));

        if (ouvrables)
        {
            dates = dates.Where(dt => dt.DayOfWeek != DayOfWeek.Saturday && dt.DayOfWeek != DayOfWeek.Sunday);
        }

        var feries = new XPQuery<JourFerie>(Session).Where(f => f.Date >= dStart && f.Date <= dEnd)
                                                    .Select(f => f.Date).ToHashSet();
        return dates.Count(d => !feries.Contains(d));
    }
}
