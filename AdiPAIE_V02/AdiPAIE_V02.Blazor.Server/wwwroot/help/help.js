// =============================================================
//  SunuPaie — Aide contextuelle
//  Emplacement : wwwroot/help/help.js
// =============================================================

window.AdiPAIE = window.AdiPAIE || {};

/**
 * Ouvre une fenêtre popup avec la page d'aide correspondant
 * au viewId XAF courant.
 *
 * Appelé via JSInterop depuis HelpController.cs :
 *   jsRuntime.InvokeVoidAsync("AdiPAIE.openHelp", viewId)
 */
window.AdiPAIE.openHelp = function (viewId) {
    var page = getHelpPage(viewId);
    var url = '/help/' + page;
    window.open(url, 'SunuPaie_Aide',
        'width=800,height=700,scrollbars=yes,resizable=yes');
};

function getHelpPage(viewId) {
    var map = {
        'Bulletin_DetailView': 'bulletins.html',
        'Bulletin_ListView': 'bulletins.html',
        'PeriodePaie_ListView': 'periodes.html',
        'PeriodePaie_DetailView': 'periodes.html',
        'Pret_DetailView': 'prets.html',
        'Pret_ListView': 'prets.html',
        'PretEcheance_ListView': 'prets.html',
        'Salarie_DetailView': 'salaries.html',
        'Salarie_ListView': 'salaries.html',
        'CongeDemande_DetailView': 'conges.html',
        'CongeDemande_ListView': 'conges.html',
        'SoldeConge_DetailView': 'conges.html',
        'SoldeConge_ListView': 'conges.html',
        'CongeType_ListView': 'conges.html',
        'DemandeDeplacement_DetailView': 'missions.html',
        'DemandeDeplacement_ListView': 'missions.html',
        'SessionFormation_DetailView': 'formations.html',
        'SessionFormation_ListView': 'formations.html',
        'PlanFormation_DetailView': 'formations.html',
        'PlanFormation_ListView': 'formations.html',
        'EntretienAnnuel_DetailView': 'evaluations.html',
        'EntretienAnnuel_ListView': 'evaluations.html',
        'DemandeAvancement_DetailView': 'avancements.html',
        'DemandeAvancement_ListView': 'avancements.html',
        'ParametresPaie_DetailView': 'parametrage.html',
        'RubriqueTypeRef_ListView': 'parametrage.html',
        'RubriqueTypeRef_DetailView': 'parametrage.html',
        'DemandeAttestation_ListView': 'index.html',
        'DemandeAttestation_DetailView': 'index.html'
    };
    return map[viewId] || 'index.html';
}
