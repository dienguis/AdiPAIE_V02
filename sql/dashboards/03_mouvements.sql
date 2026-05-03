-- ============================================================================
-- 03_mouvements.sql
-- Tableau N°3 — Mouvements (Arrivées / Départs) — Personnel INTERNE et EXTERNE
--
-- Équivalents SQL des KPI/charts implémentés en C#/XPO. Aligne avec
-- SPEC_PowerBI_DAX_to_SQL.sql (mesures 3 « Embauches » et 4 « Sorties par
-- motif »).
--
-- Conventions :
--   * Sentinelle DateSortie  : '19000101' (gère DateTime.MinValue + epoch
--     Excel + NULL après import).
--   * Année paramétrable via @annee.
--
-- ─── V1.1 (mai 2026) ─────────────────────────────────────────────────────
-- Le modèle Intérimaire a été refondu :
--   - StationService (legacy)            → Site (enrichi avec TypeSite enum)
--       TypeSite : 0=StationService, 1=Siege, 2=Depot, 3=Autre
--       Site.Nom = nom court, Site.NomAvecType = emoji + nom
--           🏪 StationService / 🏢 Siege / 📦 Depot / 📍 Autre
--   - BusinessUnitStation (legacy)       → UniteOrganisationnelle (récursive)
--       UniteOrganisationnelle.TypeUnite : 0=BU, 1=Departement, 2=Segment, 3=Autre
--   - ContratInterim.Station (legacy FK) → ContratInterim.Site (FK V1.1)
--   - ContratInterim.BU      (legacy FK) → ContratInterim.Unites (N-N XPO,
--                                          association "Contrat-Unites")
--   - MouvementInterimaire   : SiteOrigineV1 / SiteDestinationV1 / UniteOrigineV1
--                              UniteDestinationV1 (les FK V1.0 sont marquées [Legacy])
--
-- Les colonnes legacy (Station / BU / EstDG) restent en base pour
-- compatibilité mais NE SONT PLUS LUES par les services dashboards.
-- ============================================================================

DECLARE @annee int = 2025;
DECLARE @debut date = DATEFROMPARTS(@annee,  1,  1);
DECLARE @fin   date = DATEFROMPARTS(@annee, 12, 31);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  INTERNE — Salarie (CDI / CDD / Stage)                                   ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

-- 1. Nombre d'ARRIVÉES dans l'année (DateEmbauche dans [debut..fin])
SELECT COUNT(*) AS NbArrivees
FROM [dbo].[Salarie]
WHERE [DateEmbauche] >= @debut
  AND [DateEmbauche] <= @fin;

-- 2. Nombre de DÉPARTS dans l'année (DateSortie >= sentinelle ET dans [debut..fin])
SELECT COUNT(*) AS NbDeparts
FROM [dbo].[Salarie]
WHERE [DateSortie] >= '19000101'
  AND [DateSortie] >= @debut
  AND [DateSortie] <= @fin;

-- 3. Effectif au 1/1 et 31/12 + Effectif moyen (pour les taux)
WITH EffDebut AS (
    SELECT COUNT(*) AS V FROM [dbo].[Salarie]
    WHERE [DateEmbauche] <= @debut
      AND ([DateSortie] IS NULL OR [DateSortie] < '19000101' OR [DateSortie] > @debut)
),
EffFin AS (
    SELECT COUNT(*) AS V FROM [dbo].[Salarie]
    WHERE [DateEmbauche] <= @fin
      AND ([DateSortie] IS NULL OR [DateSortie] < '19000101' OR [DateSortie] > @fin)
)
SELECT
    (SELECT V FROM EffDebut) AS EffectifDebut,
    (SELECT V FROM EffFin)   AS EffectifFin,
    ((SELECT V FROM EffDebut) + (SELECT V FROM EffFin)) / 2.0 AS EffectifMoyen;

-- 4. Arrivées par MOIS (12 lignes Jan→Déc)
SELECT MONTH([DateEmbauche]) AS Mois, COUNT(*) AS NbArrivees
FROM [dbo].[Salarie]
WHERE [DateEmbauche] >= @debut AND [DateEmbauche] <= @fin
GROUP BY MONTH([DateEmbauche])
ORDER BY Mois;

-- 5. Départs par MOIS
SELECT MONTH([DateSortie]) AS Mois, COUNT(*) AS NbDeparts
FROM [dbo].[Salarie]
WHERE [DateSortie] >= '19000101'
  AND [DateSortie] >= @debut
  AND [DateSortie] <= @fin
GROUP BY MONTH([DateSortie])
ORDER BY Mois;

-- 6. Arrivées par SITE (V1.1 — TypeSite enum + emoji simulé)
SELECT
    ISNULL(si.[Nom], '(Non renseigné)')                AS SiteNom,
    CASE si.[Type]
        WHEN 0 THEN N'🏪 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 1 THEN N'🏢 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 2 THEN N'📦 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 3 THEN N'📍 ' + ISNULL(si.[Nom], '(Non renseigné)')
        ELSE        ISNULL(si.[Nom], '(Non renseigné)')
    END                                                AS SiteNomAvecType,
    si.[Type]                                          AS TypeSiteCode,
    COUNT(*)                                           AS NbArrivees
FROM [dbo].[Salarie] s
LEFT JOIN [dbo].[Site] si ON si.[Oid] = s.[Site]
WHERE s.[DateEmbauche] >= @debut AND s.[DateEmbauche] <= @fin
GROUP BY si.[Nom], si.[Type]
ORDER BY NbArrivees DESC;

-- 7. Arrivées par CATÉGORIE professionnelle
SELECT ISNULL(c.[Intitule], '(Non renseignée)') AS Categorie, COUNT(*) AS NbArrivees
FROM [dbo].[Salarie] s
LEFT JOIN [dbo].[Categories] c ON c.[Oid] = s.[Categories]
WHERE s.[DateEmbauche] >= @debut AND s.[DateEmbauche] <= @fin
GROUP BY c.[Intitule]
ORDER BY NbArrivees DESC;

-- 8. Départs par MOTIF (enum MotifDepart)
--   Mapping (cf. Domain/DomainEnums.cs) :
--     0=Démission, 1=Licenciement, 2=Retraite, 3=Fin de CDD,
--     4=Rupture conventionnelle, 5=Décès, 6=Autre, NULL=(Non renseigné)
SELECT
    CASE [MotifDepart]
        WHEN 0 THEN 'Démission'
        WHEN 1 THEN 'Licenciement'
        WHEN 2 THEN 'Retraite'
        WHEN 3 THEN 'Fin de CDD'
        WHEN 4 THEN 'Rupture conventionnelle'
        WHEN 5 THEN 'Décès'
        WHEN 6 THEN 'Autre'
        ELSE        '(Non renseigné)'
    END AS Motif,
    COUNT(*) AS NbDeparts
FROM [dbo].[Salarie]
WHERE [DateSortie] >= '19000101'
  AND [DateSortie] >= @debut
  AND [DateSortie] <= @fin
GROUP BY [MotifDepart]
ORDER BY NbDeparts DESC;

-- 9. Départs par SITE (V1.1)
SELECT
    ISNULL(si.[Nom], '(Non renseigné)') AS SiteNom,
    CASE si.[Type]
        WHEN 0 THEN N'🏪 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 1 THEN N'🏢 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 2 THEN N'📦 ' + ISNULL(si.[Nom], '(Non renseigné)')
        ELSE        ISNULL(si.[Nom], '(Non renseigné)')
    END AS SiteNomAvecType,
    COUNT(*) AS NbDeparts
FROM [dbo].[Salarie] s
LEFT JOIN [dbo].[Site] si ON si.[Oid] = s.[Site]
WHERE s.[DateSortie] >= '19000101'
  AND s.[DateSortie] >= @debut
  AND s.[DateSortie] <= @fin
GROUP BY si.[Nom], si.[Type]
ORDER BY NbDeparts DESC;

-- 10. Départs par CATÉGORIE
SELECT ISNULL(c.[Intitule], '(Non renseignée)') AS Categorie, COUNT(*) AS NbDeparts
FROM [dbo].[Salarie] s
LEFT JOIN [dbo].[Categories] c ON c.[Oid] = s.[Categories]
WHERE s.[DateSortie] >= '19000101'
  AND s.[DateSortie] >= @debut
  AND s.[DateSortie] <= @fin
GROUP BY c.[Intitule]
ORDER BY NbDeparts DESC;


-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  EXTERNE — ContratInterim (Intérimaires) — V1.1                          ║
-- ║  Source équivalente côté C# : MouvementsDashboardService.ComputeForExterne
-- ╚══════════════════════════════════════════════════════════════════════════╝

-- 11. Arrivées EXTERNES = ContratInterim.DateDebut dans l'année
SELECT COUNT(*) AS NbArriveesExt
FROM [dbo].[ContratInterim]
WHERE [DateDebut] >= @debut AND [DateDebut] <= @fin;

-- 12. Départs EXTERNES = ContratInterim clôturés (Statut Resilie/Termine)
--     dont DateFinReelle (ou DateFin si null) tombe dans l'année.
--     Mapping ContratInterimStatut (cf. DomainEnums.cs) :
--       0=Brouillon, 1=EnCours, 2=Termine, 3=Resilie
SELECT COUNT(*) AS NbDepartsExt
FROM [dbo].[ContratInterim]
WHERE [Statut] IN (2, 3)
  AND COALESCE([DateFinReelle], [DateFin]) >= @debut
  AND COALESCE([DateFinReelle], [DateFin]) <= @fin;

-- 13. Arrivées EXTERNES par MOIS
SELECT MONTH([DateDebut]) AS Mois, COUNT(*) AS NbArrivees
FROM [dbo].[ContratInterim]
WHERE [DateDebut] >= @debut AND [DateDebut] <= @fin
GROUP BY MONTH([DateDebut])
ORDER BY Mois;

-- 14. Départs EXTERNES par MOIS
SELECT MONTH(COALESCE([DateFinReelle], [DateFin])) AS Mois, COUNT(*) AS NbDeparts
FROM [dbo].[ContratInterim]
WHERE [Statut] IN (2, 3)
  AND COALESCE([DateFinReelle], [DateFin]) >= @debut
  AND COALESCE([DateFinReelle], [DateFin]) <= @fin
GROUP BY MONTH(COALESCE([DateFinReelle], [DateFin]))
ORDER BY Mois;

-- 15. Arrivées EXTERNES par SITE (V1.1 — Site enrichi avec TypeSite)
--     ⚠️ V1.1 : utilise ci.Site (FK nouvelle) et NON ci.Station (legacy).
--     Le service C# fait la même chose dans MouvementsDashboardService.
SELECT
    ISNULL(si.[Nom], '(Non renseigné)')                AS SiteNom,
    CASE si.[Type]
        WHEN 0 THEN N'🏪 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 1 THEN N'🏢 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 2 THEN N'📦 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 3 THEN N'📍 ' + ISNULL(si.[Nom], '(Non renseigné)')
        ELSE        ISNULL(si.[Nom], '(Non renseigné)')
    END                                                AS SiteNomAvecType,
    si.[Type]                                          AS TypeSiteCode,
    COUNT(*)                                           AS NbArrivees
FROM [dbo].[ContratInterim] ci
LEFT JOIN [dbo].[Site] si ON si.[Oid] = ci.[Site]
WHERE ci.[DateDebut] >= @debut AND ci.[DateDebut] <= @fin
GROUP BY si.[Nom], si.[Type]
ORDER BY NbArrivees DESC;

-- 16. Arrivées EXTERNES par POSTE
SELECT ISNULL(p.[Libelle], '(Non renseigné)') AS Poste, COUNT(*) AS NbArrivees
FROM [dbo].[ContratInterim] ci
LEFT JOIN [dbo].[PosteInterimaire] p ON p.[Oid] = ci.[PosteOccupe]
WHERE ci.[DateDebut] >= @debut AND ci.[DateDebut] <= @fin
GROUP BY p.[Libelle]
ORDER BY NbArrivees DESC;

-- 17. Départs EXTERNES par STATUT (Resilie / Termine)
SELECT
    CASE [Statut]
        WHEN 2 THEN 'Terminé'
        WHEN 3 THEN 'Résilié'
        ELSE        'Autre'
    END AS Statut,
    COUNT(*) AS NbDeparts
FROM [dbo].[ContratInterim]
WHERE [Statut] IN (2, 3)
  AND COALESCE([DateFinReelle], [DateFin]) >= @debut
  AND COALESCE([DateFinReelle], [DateFin]) <= @fin
GROUP BY [Statut]
ORDER BY NbDeparts DESC;

-- 18. (BONUS V1.1) Arrivées EXTERNES par UNITÉ ORGANISATIONNELLE
--     Multi-affectation N-N : un contrat peut être rattaché à plusieurs unités.
--     XPO génère automatiquement une table de jointure pour l'association
--     "Contrat-Unites" (cf. Interimaire.cs ligne 417).
--     Le nom exact dépend de la convention XPO. Pour le retrouver :
--
--       SELECT TABLE_NAME FROM INFORMATION_SCHEMA.COLUMNS
--       WHERE COLUMN_NAME IN ('ContratInterim', 'UniteOrganisationnelle')
--       GROUP BY TABLE_NAME HAVING COUNT(*) = 2;
--
--     Convention XPO probable : [ContratInterimUniteOrganisationnelle]
--     Adapter le nom ci-dessous selon la BDD réelle.

/* DÉCOMMENTER UNE FOIS LE NOM DE LA TABLE CONFIRMÉ :

SELECT
    ISNULL(u.[Nom], '(Sans unité)')      AS Unite,
    CASE u.[TypeUnite]
        WHEN 0 THEN N'🔵 BU'
        WHEN 1 THEN N'🟢 Département'
        WHEN 2 THEN N'🟡 Segment'
        WHEN 3 THEN N'⚪ Autre'
        ELSE        N'(Non typée)'
    END                                  AS TypeUnite,
    COUNT(DISTINCT ci.[Oid])             AS NbContrats,
    COUNT(DISTINCT ci.[Interimaire])     AS NbInterims
FROM [dbo].[ContratInterim] ci
LEFT JOIN [dbo].[ContratInterimUniteOrganisationnelle] cu  -- ← ajuster nom de table
    ON cu.[ContratInterim] = ci.[Oid]
LEFT JOIN [dbo].[UniteOrganisationnelle] u
    ON u.[Oid] = cu.[UniteOrganisationnelle]
WHERE ci.[DateDebut] >= @debut AND ci.[DateDebut] <= @fin
GROUP BY u.[Nom], u.[TypeUnite]
ORDER BY NbContrats DESC;

*/

-- 19. (BONUS V1.1) Mouvements internes entre sites (entité MouvementInterimaire)
--     V1.1 : utilise SiteOrigineV1 / SiteDestinationV1 (les FK SiteOrigine /
--     SiteDestination V1.0 sont marquées [Legacy] et conservées pour compat.)
SELECT
    ISNULL(so.[Nom], '(Non renseigné)') AS SiteOrigine,
    ISNULL(sd.[Nom], '(Non renseigné)') AS SiteDestination,
    COUNT(*)                            AS NbMouvements
FROM [dbo].[MouvementInterimaire] m
LEFT JOIN [dbo].[Site] so ON so.[Oid] = m.[SiteOrigineV1]
LEFT JOIN [dbo].[Site] sd ON sd.[Oid] = m.[SiteDestinationV1]
WHERE m.[DateMouvement] >= @debut AND m.[DateMouvement] <= @fin
GROUP BY so.[Nom], sd.[Nom]
ORDER BY NbMouvements DESC;
