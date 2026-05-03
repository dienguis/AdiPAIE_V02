-- ============================================================================
-- 05_suivi_absences.sql
-- Tableau N°5 — Suivi des Absences (INTERNE uniquement)
--
-- Source : entité CongeDemande (Salarie + Type → CongeType.Famille)
-- Statut par défaut : Accordee (20)
--
-- Mapping CongeStatut (cf. Domain/DomainEnums.cs) :
--    0=Brouillon, 1=EnAttenteN1, 2=EnAttenteN2, 10=Soumise,
--    20=Accordee,  30=Refusee,    40=Annulee
--
-- Mapping FamilleConge :
--    0=Annuel, 1=Maladie, 2=Maternite, 3=EvenementFamilial,
--    4=SansSolde, 5=Recuperation
-- ============================================================================

DECLARE @annee int = 2025;
DECLARE @debut date = DATEFROMPARTS(@annee,  1,  1);
DECLARE @fin   date = DATEFROMPARTS(@annee, 12, 31);

-- 1. KPI globaux : Nb absences + Total jours + durée moyenne
SELECT
    COUNT(*)                         AS NbAbsences,
    SUM(c.[DureeJours])              AS TotalJours,
    AVG(c.[DureeJours])              AS DureeMoyenne,
    100.0 * SUM(CASE WHEN c.[JustificatifFourni] = 1 THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0) AS PctJustifiees,
    COUNT(DISTINCT c.[Salarie])      AS NbSalariesAbsents
FROM [dbo].[CongeDemande] c
WHERE c.[Statut]   = 20                       -- Accordee
  AND c.[DateDebut] <= @fin
  AND c.[DateFin]   >= @debut;

-- 2. Effectif au 31/12 (dénominateur du taux d'absentéisme)
SELECT COUNT(*) AS EffectifFin
FROM [dbo].[Salarie] s
WHERE s.[DateEmbauche] <= @fin
  AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > @fin);

-- 3. Taux d'absentéisme = TotalJours / (Effectif × 264 j ouvrables)
WITH Tot AS (
    SELECT SUM(c.[DureeJours]) AS Jours
    FROM [dbo].[CongeDemande] c
    WHERE c.[Statut] = 20
      AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
),
Eff AS (
    SELECT COUNT(*) AS N
    FROM [dbo].[Salarie] s
    WHERE s.[DateEmbauche] <= @fin
      AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > @fin)
)
SELECT
    (SELECT Jours FROM Tot) AS TotalJours,
    (SELECT N FROM Eff)     AS EffectifFin,
    CAST(
        (SELECT Jours FROM Tot) * 100.0 /
        NULLIF((SELECT N FROM Eff) * 264.0, 0)
    AS DECIMAL(6,2)) AS TauxAbsenteisme_Pct;

-- 4. Répartition par FAMILLE de congé (Annuel / Maladie / ...)
SELECT
    CASE ct.[Famille]
        WHEN 0 THEN N'Congé annuel'
        WHEN 1 THEN N'Maladie'
        WHEN 2 THEN N'Maternité / Paternité'
        WHEN 3 THEN N'Événement familial'
        WHEN 4 THEN N'Sans solde'
        WHEN 5 THEN N'Récupération'
        ELSE        N'(Autres)'
    END AS Famille,
    COUNT(*)            AS NbAbsences,
    SUM(c.[DureeJours]) AS TotalJours
FROM [dbo].[CongeDemande] c
LEFT JOIN [dbo].[CongeType] ct ON ct.[Oid] = c.[Type]
WHERE c.[Statut] = 20
  AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
GROUP BY ct.[Famille]
ORDER BY TotalJours DESC;

-- 5. Répartition par CATÉGORIE professionnelle (Top 10)
SELECT TOP 10
    ISNULL(cat.[Intitule], '(Non renseignée)') AS Categorie,
    COUNT(*) AS NbAbsences,
    SUM(c.[DureeJours]) AS TotalJours
FROM [dbo].[CongeDemande] c
INNER JOIN [dbo].[Salarie]    s   ON s.[Oid] = c.[Salarie]
LEFT  JOIN [dbo].[Categories] cat ON cat.[Oid] = s.[Categories]
WHERE c.[Statut] = 20
  AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
GROUP BY cat.[Intitule]
ORDER BY TotalJours DESC;

-- 6. Répartition par DÉPARTEMENT (Segment)
SELECT
    ISNULL(d.[Nom], '(Non renseigné)') AS Segment,
    COUNT(*) AS NbAbsences,
    SUM(c.[DureeJours]) AS TotalJours
FROM [dbo].[CongeDemande] c
INNER JOIN [dbo].[Salarie]    s ON s.[Oid] = c.[Salarie]
LEFT  JOIN [dbo].[Departement] d ON d.[Oid] = s.[Departement]
WHERE c.[Statut] = 20
  AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
GROUP BY d.[Nom]
ORDER BY TotalJours DESC;

-- 7. Évolution mensuelle (12 mois) — utilise le mois de début pour simplifier
SELECT
    MONTH(c.[DateDebut]) AS Mois,
    COUNT(*)             AS NbAbsences,
    SUM(c.[DureeJours])  AS NbJours
FROM [dbo].[CongeDemande] c
WHERE c.[Statut] = 20
  AND YEAR(c.[DateDebut]) = @annee
GROUP BY MONTH(c.[DateDebut])
ORDER BY Mois;

-- 8. Top 10 salariés les plus absents
SELECT TOP 10
    p.[FirstName] + ' ' + p.[LastName] AS Salarie,
    cat.[Intitule]                     AS Categorie,
    d.[Nom]                            AS Segment,
    COUNT(*)                           AS NbAbsences,
    SUM(c.[DureeJours])                AS TotalJours
FROM [dbo].[CongeDemande] c
INNER JOIN [dbo].[Salarie]    s   ON s.[Oid] = c.[Salarie]
INNER JOIN [dbo].[Person]     p   ON p.[Oid] = s.[Oid]
LEFT  JOIN [dbo].[Categories] cat ON cat.[Oid] = s.[Categories]
LEFT  JOIN [dbo].[Departement] d  ON d.[Oid] = s.[Departement]
WHERE c.[Statut] = 20
  AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
GROUP BY p.[FirstName], p.[LastName], cat.[Intitule], d.[Nom]
ORDER BY TotalJours DESC;
