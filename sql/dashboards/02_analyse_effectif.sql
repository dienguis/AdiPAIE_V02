-- ============================================================================
-- 02_analyse_effectif.sql
-- Tableau N°2 — Analyse de l'Effectif (Personnel INTERNE et EXTERNE)
--
-- Équivalents SQL des KPI/charts implémentés en C#/XPO. Aligne avec
-- SPEC_PowerBI_DAX_to_SQL.sql (mesures 1, 3, 4, 5).
-- ============================================================================

DECLARE @annee int = 2025;
DECLARE @dateRef date = CASE WHEN @annee = YEAR(GETDATE())
                             THEN CAST(GETDATE() AS date)
                             ELSE DATEFROMPARTS(@annee, 12, 31) END;

-- 1. Effectif Global à dateRef
SELECT COUNT(*) AS EffectifGlobal
FROM [dbo].[Salarie] s
WHERE s.[DateEmbauche] <= @dateRef
  AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > @dateRef);

-- 2. Effectif Moyen Annuel = (effectif au 1/1 + effectif au 31/12) / 2
SELECT
  ((SELECT COUNT(*) FROM [dbo].[Salarie]
    WHERE [DateEmbauche] <= DATEFROMPARTS(@annee, 1, 1)
      AND ([DateSortie] IS NULL OR [DateSortie] < '19000101' OR [DateSortie] > DATEFROMPARTS(@annee, 1, 1)))
   +
   (SELECT COUNT(*) FROM [dbo].[Salarie]
    WHERE [DateEmbauche] <= DATEFROMPARTS(@annee, 12, 31)
      AND ([DateSortie] IS NULL OR [DateSortie] < '19000101' OR [DateSortie] > DATEFROMPARTS(@annee, 12, 31)))
  ) / 2.0 AS EffectifMoyen;

-- 3. % Départs et % Rotation (turnover) — cf. mesure 5 du SPEC_PowerBI
-- (calcul : Sorties / Effectif (instantané ou moyen))

-- 4. KPI démographiques (avg age, avg ancienneté, % H/F)
SELECT
    AVG(DATEDIFF(YEAR, p.[Birthday], @dateRef))      AS AgeMoyen,
    AVG(DATEDIFF(YEAR, s.[DateEmbauche], @dateRef))  AS AncienneteMoyenne,
    SUM(CASE WHEN s.[Sexe] = 0 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS PctHommes,
    SUM(CASE WHEN s.[Sexe] = 1 THEN 1 ELSE 0 END) * 100.0 / COUNT(*) AS PctFemmes
FROM [dbo].[Salarie] s
INNER JOIN [dbo].[Person] p ON s.[Oid] = p.[Oid]
WHERE s.[DateEmbauche] <= @dateRef
  AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > @dateRef);

-- 5. Évolution effectif sur 8 années glissantes
;WITH Annees AS (
    SELECT (YEAR(@dateRef) - 7) AS Annee
    UNION ALL SELECT Annee + 1 FROM Annees WHERE Annee + 1 <= YEAR(@dateRef)
)
SELECT a.Annee,
       (SELECT COUNT(*) FROM [dbo].[Salarie] s
        WHERE s.[DateEmbauche] <= DATEFROMPARTS(a.Annee, 12, 31)
          AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101'
               OR s.[DateSortie] > DATEFROMPARTS(a.Annee, 12, 31))) AS Effectif
FROM Annees a
ORDER BY a.Annee;

-- 6. Bar chart Tranche d'âge (4 + vide)
SELECT
    CASE
        WHEN p.[Birthday] IS NULL OR p.[Birthday] < '19000101' THEN N'Vide'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 30  THEN N'<30'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 40  THEN N'30-39'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 50  THEN N'40-49'
        ELSE                                                   N'≥50'
    END AS Tranche,
    COUNT(*) AS Effectif
FROM [dbo].[Salarie] s
INNER JOIN [dbo].[Person] p ON s.[Oid] = p.[Oid]
WHERE s.[DateEmbauche] <= @dateRef
  AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > @dateRef)
GROUP BY
    CASE
        WHEN p.[Birthday] IS NULL OR p.[Birthday] < '19000101' THEN N'Vide'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 30  THEN N'<30'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 40  THEN N'30-39'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 50  THEN N'40-49'
        ELSE                                                   N'≥50'
    END;

-- 7. Bar chart Ancienneté (5 + vide), Segment (Département), Catégorie Top 5,
--    Type contrat — voir AnalyseEffectifDashboardService.cs pour le code C#.
