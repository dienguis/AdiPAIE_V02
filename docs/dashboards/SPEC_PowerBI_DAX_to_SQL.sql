-- ============================================================
-- SPEC_PowerBI_DAX_to_SQL.sql
--
-- Cahier des charges fourni par l'utilisateur (Abdoulaye Dieng) — équivalents
-- SQL des mesures DAX PowerBI à reproduire dans le custom dashboard SunuPaie
-- (.NET 8 / Blazor / DevExpress XAF 25.1.10 / XPO).
--
-- Ce fichier sert de RÉFÉRENCE pour les services Dashboards (Étapes 4.1 → 4.6).
-- Le code C#/XPO doit produire des résultats équivalents (au type / précision
-- près) à ces requêtes SQL exécutées sur la base SunuPaie.
--
-- Paramètres communs :
--   @annee int       : année de référence (ex: 2025)
--   @dateRef date    : date de référence (ex: '2025-12-31')
--   @site nvarchar   : code site optionnel ('SIEGE', 'DEPOT_CDB', etc.)
--   @categorie nvarchar : libellé catégorie optionnel
--
-- Mapping mesures → Tableaux mission :
--   1. Effectif total ............. Tableaux 1, 2, 6
--   2. Effectif fin mois .......... Tableau 6 (Bilan Social Mensuel)
--   3. Embauches sur année ........ Tableaux 3 (Arrivées), 6
--   4. Sorties (motifs) ........... Tableaux 3 (Départs), 6
--   5. Turnover % ................. Tableaux 2, 6
--   6. Masse Brute ................ Tableaux 4, 6
--   7. Coût Employeur ............. Tableau 6
--   8. Écart Salarial H/F ......... Tableau 4 (Égalité salariale)
--   9. Récap mensuel 12 mois ...... Tableau 6
--  10. Décomposition rubriques ... Tableau 4 (Rémunération détaillée)
--  11. Pyramide des âges ......... Tableau 1 (déjà fait), 2
--
-- ============================================================
SET DATEFORMAT ymd;

-- ============================================================
-- 1. EFFECTIF TOTAL au 31/12 d'une année
-- ============================================================
DECLARE @dateRef date = '2025-12-31';
SELECT COUNT(DISTINCT s.[Oid]) AS Effectif_Total
FROM [dbo].[Salarie] s
WHERE (s.[DateEmbauche] IS NULL OR s.[DateEmbauche] <= @dateRef)
  AND (s.[DateSortie]   IS NULL
       OR s.[DateSortie] < '19000101'
       OR s.[DateSortie] > @dateRef);

-- ============================================================
-- 2. EFFECTIF FIN MOIS (pour récap mensuel)
-- ============================================================
DECLARE @annee int = 2025, @mois int = 8;
DECLARE @eom date = EOMONTH(DATEFROMPARTS(@annee, @mois, 1));
SELECT COUNT(DISTINCT s.[Oid]) AS Effectif_FinMois
FROM [dbo].[Salarie] s
WHERE (s.[DateEmbauche] IS NULL OR s.[DateEmbauche] <= @eom)
  AND (s.[DateSortie]   IS NULL
       OR s.[DateSortie] < '19000101'
       OR s.[DateSortie] > @eom);

-- ============================================================
-- 3. EMBAUCHES SUR UNE ANNÉE
-- ============================================================
SELECT COUNT(*) AS Nb_Embauches
FROM [dbo].[Salarie]
WHERE YEAR([DateEmbauche]) = @annee;

-- ============================================================
-- 4. SORTIES SUR UNE ANNÉE (motifs inclus)
-- ============================================================
SELECT
    CASE [MotifDepart]
        WHEN 0 THEN N'Démission'
        WHEN 1 THEN N'Licenciement'
        WHEN 2 THEN N'Fin de CDD'
        WHEN 3 THEN N'Retraite'
        WHEN 4 THEN N'Décès'
        WHEN 5 THEN N'Rupture conventionnelle'
        ELSE N'Autre'
    END AS Motif,
    COUNT(*) AS Nb
FROM [dbo].[Salarie]
WHERE [DateSortie] IS NOT NULL
  AND [DateSortie] >= '19000101'
  AND YEAR([DateSortie]) = @annee
GROUP BY [MotifDepart]
ORDER BY 2 DESC;

-- ============================================================
-- 5. TURNOVER % = Sorties / Effectif Moyen Annuel
-- ============================================================
SELECT
    @annee AS Annee,
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateSortie]) = @annee
       AND [DateSortie] >= '19000101') AS Sorties,
    (
        (SELECT COUNT(*) FROM [dbo].[Salarie]
         WHERE [DateEmbauche] <= DATEFROMPARTS(@annee, 1, 1)
           AND ([DateSortie] IS NULL OR [DateSortie] > DATEFROMPARTS(@annee, 1, 1)))
      +
        (SELECT COUNT(*) FROM [dbo].[Salarie]
         WHERE [DateEmbauche] <= DATEFROMPARTS(@annee, 12, 31)
           AND ([DateSortie] IS NULL OR [DateSortie] > DATEFROMPARTS(@annee, 12, 31)))
    ) / 2.0 AS EffectifMoyen,
    CAST(
        (SELECT COUNT(*) FROM [dbo].[Salarie]
         WHERE YEAR([DateSortie]) = @annee
           AND [DateSortie] >= '19000101') * 100.0
        /
        NULLIF((
            (SELECT COUNT(*) FROM [dbo].[Salarie]
             WHERE [DateEmbauche] <= DATEFROMPARTS(@annee, 1, 1)
               AND ([DateSortie] IS NULL OR [DateSortie] > DATEFROMPARTS(@annee, 1, 1)))
          +
            (SELECT COUNT(*) FROM [dbo].[Salarie]
             WHERE [DateEmbauche] <= DATEFROMPARTS(@annee, 12, 31)
               AND ([DateSortie] IS NULL OR [DateSortie] > DATEFROMPARTS(@annee, 12, 31)))
        ) / 2.0, 0) AS DECIMAL(5,2)
    ) AS Turnover_Pct;

-- ============================================================
-- 6. MASSE BRUTE annuelle
-- ============================================================
SELECT
    b.[Annee],
    SUM(b.[BrutFiscal])  AS MasseBrute,
    SUM(b.[NetAPayer])   AS NetTotal,
    SUM(b.[TotalCotisationsSociales]) AS CotisationsSociales,
    SUM(b.[TotalRetenuesFiscales])    AS RetenuesFiscales
FROM [dbo].[Bulletin] b
WHERE b.[GCRecord] IS NULL
  AND b.[Annee] = @annee
GROUP BY b.[Annee];

-- ============================================================
-- 7. COÛT EMPLOYEUR = Masse Brute + Charges Patronales
-- ============================================================
SELECT
    SUM(b.[BrutFiscal]) AS MasseBrute,
    ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b2 ON bl.[Bulletin] = b2.[Oid]
        WHERE b2.[Annee] = @annee AND b2.[GCRecord] IS NULL
    ), 0) AS ChargesPatronales,
    SUM(b.[BrutFiscal]) + ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b2 ON bl.[Bulletin] = b2.[Oid]
        WHERE b2.[Annee] = @annee AND b2.[GCRecord] IS NULL
    ), 0) AS CoutEmployeur
FROM [dbo].[Bulletin] b
WHERE b.[Annee] = @annee
  AND b.[GCRecord] IS NULL;

-- ============================================================
-- 8. ÉCART SALARIAL H/F par CATÉGORIE (Égalité salariale)
-- ============================================================
SELECT
    cat.[Intitule] AS Categorie,
    AVG(CASE WHEN s.[Sexe] = 0 THEN b.[BrutFiscal] END) AS BrutMoyen_H,
    AVG(CASE WHEN s.[Sexe] = 1 THEN b.[BrutFiscal] END) AS BrutMoyen_F,
    AVG(b.[BrutFiscal]) AS BrutMoyen_Total,
    MIN(b.[BrutFiscal]) AS BrutMin,
    MAX(b.[BrutFiscal]) AS BrutMax,
    CAST(
        (AVG(CASE WHEN s.[Sexe] = 1 THEN b.[BrutFiscal] END)
         - AVG(CASE WHEN s.[Sexe] = 0 THEN b.[BrutFiscal] END)) * 100.0
        / NULLIF(AVG(CASE WHEN s.[Sexe] = 0 THEN b.[BrutFiscal] END), 0)
        AS DECIMAL(5,2)
    ) AS Ecart_HF_Pct
FROM [dbo].[Bulletin] b
    INNER JOIN [dbo].[Salarie]    s   ON b.[Salarie]    = s.[Oid]
    LEFT  JOIN [dbo].[Categories] cat ON s.[Categories] = cat.[Oid]
WHERE b.[GCRecord] IS NULL
GROUP BY cat.[Intitule]
ORDER BY cat.[Intitule];

-- ============================================================
-- 9. RÉCAP MENSUEL (12 mois x indicateurs) — Tableau 6
-- ============================================================
WITH MonthlyData AS (
    SELECT
        b.[Annee],
        b.[Mois],
        DATEFROMPARTS(b.[Annee], b.[Mois], 1) AS DateMois,
        EOMONTH(DATEFROMPARTS(b.[Annee], b.[Mois], 1)) AS EOM,
        SUM(b.[BrutFiscal]) AS MasseBrute_Mois,
        COUNT(DISTINCT b.[Salarie]) AS NbBulletins
    FROM [dbo].[Bulletin] b
    WHERE b.[Annee] = @annee
    GROUP BY b.[Annee], b.[Mois]
)
SELECT
    md.[Annee],
    md.[Mois],
    (SELECT COUNT(*) FROM [dbo].[Salarie] s
     WHERE s.[DateEmbauche] <= md.EOM
       AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > md.EOM)
    ) AS EffectifFinMois,
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateEmbauche]) = md.[Annee] AND MONTH([DateEmbauche]) = md.[Mois]
    ) AS EmbauchesMois,
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE [DateSortie] >= '19000101'
       AND YEAR([DateSortie]) = md.[Annee] AND MONTH([DateSortie]) = md.[Mois]
    ) AS SortiesMois,
    md.MasseBrute_Mois,
    ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
        WHERE b.[Annee] = md.[Annee] AND b.[Mois] = md.[Mois]
    ), 0) AS ChargesPatronales_Mois
FROM MonthlyData md
ORDER BY md.[Annee], md.[Mois];

-- ============================================================
-- 10. DÉCOMPOSITION PAR FAMILLE DE RUBRIQUE — Tableau 4
-- ============================================================
SELECT
    CASE rtr.[Code]
        WHEN 'BRUTE'                    THEN N'1. Salaire de base & primes'
        WHEN 'INDEM_IMPOSA'             THEN N'2. Indemnités imposables'
        WHEN 'INDEM_NON_IMPOSA'         THEN N'3. Indemnités non imposables'
        WHEN 'AV_NATURE_IMPOSABLE'      THEN N'4. Avantages en nature'
        WHEN 'AvNatImpos'               THEN N'4. Avantages en nature'
        WHEN 'AV_NATURE_NON_IMPOSABLE'  THEN N'4. Avantages en nature'
        WHEN 'COTSOC'                   THEN N'5. Cotisations sociales'
        WHEN 'COTFISC'                  THEN N'6. Cotisations fiscales'
        WHEN 'RETENUE'                  THEN N'7. Retenues diverses'
        ELSE                                 N'(non défini)'
    END AS FamilleMacro,
    SUM(bl.[Montant])         AS TotalSalarial,
    SUM(bl.[MontantEmployeur]) AS TotalEmployeur,
    SUM(bl.[Montant] + ISNULL(bl.[MontantEmployeur], 0)) AS TotalCombined
FROM [dbo].[BulletinLigne] bl
    INNER JOIN [dbo].[Bulletin]        b   ON bl.[Bulletin] = b.[Oid]
    INNER JOIN [dbo].[Rubrique]        r   ON bl.[Rubrique] = r.[Oid]
    LEFT  JOIN [dbo].[RubriqueTypeRef] rtr ON r.[TypeRef]   = rtr.[Oid]
WHERE bl.[GCRecord] IS NULL
  AND b.[GCRecord]  IS NULL
  AND b.[Annee] = @annee
GROUP BY rtr.[Code]
ORDER BY 1;

-- ============================================================
-- 11. PYRAMIDE DES ÂGES (par tranche × sexe)
-- ============================================================
SELECT
    CASE
        WHEN p.[Birthday] IS NULL OR p.[Birthday] < '19000101' THEN N'(non défini)'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 25  THEN N'< 25 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 35  THEN N'25 - 34 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 45  THEN N'35 - 44 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 55  THEN N'45 - 54 ans'
        ELSE                                                    N'55 ans et plus'
    END AS TrancheAge,
    SUM(CASE WHEN s.[Sexe] = 0 THEN 1 ELSE 0 END) AS Hommes,
    SUM(CASE WHEN s.[Sexe] = 1 THEN 1 ELSE 0 END) AS Femmes,
    COUNT(*) AS Total
FROM [dbo].[Salarie] s
    INNER JOIN [dbo].[Person] p ON s.[Oid] = p.[Oid]
WHERE (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > @dateRef)
GROUP BY
    CASE
        WHEN p.[Birthday] IS NULL OR p.[Birthday] < '19000101' THEN N'(non défini)'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 25  THEN N'< 25 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 35  THEN N'25 - 34 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 45  THEN N'35 - 44 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], GETDATE()) < 55  THEN N'45 - 54 ans'
        ELSE                                                    N'55 ans et plus'
    END
ORDER BY MIN(DATEDIFF(YEAR, p.[Birthday], GETDATE()));
