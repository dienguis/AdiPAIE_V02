-- ============================================================================
-- 04_remuneration.sql
-- Tableau N°4 — Rémunération (Égalité des salaires)
--
-- Équivalents SQL des KPI/charts implémentés en C#/XPO. Aligne avec :
--   - SPEC_PowerBI_DAX_to_SQL.sql (mesures 6, 7, 8, 9, 10)
--   - TestData/PowerBI/SPEC_Custom_Dashboard_SQL.sql (référence détaillée
--     fournie par l'utilisateur)
--
-- ============================================================================

DECLARE @annee int = 2025;
DECLARE @debut date = DATEFROMPARTS(@annee,  1,  1);
DECLARE @fin   date = DATEFROMPARTS(@annee, 12, 31);

-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  INTERNE — Bulletin / BulletinLigne                                      ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

-- 1. Masse Brute annuelle + Net + Cotisations
SELECT
    SUM(b.[BrutFiscal])                AS MasseBrute,
    SUM(b.[NetAPayer])                 AS NetTotal,
    SUM(b.[TotalCotisationsSociales])  AS CotisationsSociales,
    SUM(b.[TotalRetenuesFiscales])     AS RetenuesFiscales
FROM [dbo].[Bulletin] b
WHERE b.[GCRecord] IS NULL
  AND b.[Annee]    = @annee;

-- 2. Coût Employeur = Masse Brute + Charges Patronales
SELECT
    SUM(b.[BrutFiscal]) AS MasseBrute,
    ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b2 ON bl.[Bulletin] = b2.[Oid]
        WHERE b2.[Annee] = @annee
          AND b2.[GCRecord] IS NULL
          AND bl.[GCRecord] IS NULL
    ), 0) AS ChargesPatronales,
    SUM(b.[BrutFiscal]) + ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b2 ON bl.[Bulletin] = b2.[Oid]
        WHERE b2.[Annee] = @annee
          AND b2.[GCRecord] IS NULL
          AND bl.[GCRecord] IS NULL
    ), 0) AS CoutEmployeur
FROM [dbo].[Bulletin] b
WHERE b.[Annee] = @annee
  AND b.[GCRecord] IS NULL;

-- 3. KPI globaux — Min / Max / Moyen sur BrutFiscal
SELECT
    MIN(b.[BrutFiscal]) AS SalaireMin,
    MAX(b.[BrutFiscal]) AS SalaireMax,
    AVG(b.[BrutFiscal]) AS SalaireMoyen,
    COUNT(DISTINCT b.[Salarie]) AS NbSalariesDistincts,
    COUNT(*)            AS NbBulletins
FROM [dbo].[Bulletin] b
WHERE b.[GCRecord] IS NULL
  AND b.[Annee]    = @annee;

-- 4. Égalité des salaires par SEGMENT (Departement)
SELECT
    ISNULL(d.[Nom], '(Non renseigné)') AS Segment,
    SUM(b.[BrutFiscal])                AS Total,
    SUM(b.[BrutFiscal]) / NULLIF(COUNT(DISTINCT b.[Salarie]), 0)         AS CoutMoyen,
    MIN(b.[BrutFiscal])                AS Min_,
    MAX(b.[BrutFiscal])                AS Max_,
    AVG(b.[BrutFiscal])                AS Moyenne,
    AVG(CASE WHEN s.[Sexe] = 1 THEN b.[BrutFiscal] END) AS MoyFemme,
    AVG(CASE WHEN s.[Sexe] = 0 THEN b.[BrutFiscal] END) AS MoyHomme,
    COUNT(DISTINCT b.[Salarie])        AS NbSalaries
FROM [dbo].[Bulletin] b
    INNER JOIN [dbo].[Salarie]    s ON b.[Salarie]    = s.[Oid]
    LEFT  JOIN [dbo].[Departement] d ON s.[Departement] = d.[Oid]
WHERE b.[GCRecord] IS NULL
  AND b.[Annee]    = @annee
GROUP BY d.[Nom]
ORDER BY Total DESC;

-- 5. Égalité des salaires par CATÉGORIE professionnelle
SELECT
    ISNULL(cat.[Intitule], '(Non renseignée)') AS Categorie,
    SUM(b.[BrutFiscal])                        AS Total,
    SUM(b.[BrutFiscal]) / NULLIF(COUNT(DISTINCT b.[Salarie]), 0) AS CoutMoyen,
    MIN(b.[BrutFiscal])                        AS Min_,
    MAX(b.[BrutFiscal])                        AS Max_,
    AVG(b.[BrutFiscal])                        AS Moyenne,
    AVG(CASE WHEN s.[Sexe] = 1 THEN b.[BrutFiscal] END) AS MoyFemme,
    AVG(CASE WHEN s.[Sexe] = 0 THEN b.[BrutFiscal] END) AS MoyHomme,
    COUNT(DISTINCT b.[Salarie])                AS NbSalaries
FROM [dbo].[Bulletin] b
    INNER JOIN [dbo].[Salarie]    s   ON b.[Salarie]    = s.[Oid]
    LEFT  JOIN [dbo].[Categories] cat ON s.[Categories] = cat.[Oid]
WHERE b.[GCRecord] IS NULL
  AND b.[Annee]    = @annee
GROUP BY cat.[Intitule]
ORDER BY Total DESC;

-- 6. Évolution mensuelle 12 mois (masse brute)
SELECT
    b.[Annee], b.[Mois],
    SUM(b.[BrutFiscal])         AS MasseBrute_Mois,
    COUNT(DISTINCT b.[Salarie]) AS NbBulletins
FROM [dbo].[Bulletin] b
WHERE b.[GCRecord] IS NULL
  AND b.[Annee]    = @annee
GROUP BY b.[Annee], b.[Mois]
ORDER BY b.[Mois];

-- 7. Décomposition par famille de rubrique (cf. SPEC mesure 10)
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
        ELSE                                 N'8. (Non défini)'
    END AS FamilleMacro,
    SUM(bl.[Montant])                     AS TotalSalarial,
    SUM(bl.[MontantEmployeur])            AS TotalEmployeur,
    SUM(bl.[Montant] + ISNULL(bl.[MontantEmployeur], 0)) AS TotalCombine
FROM [dbo].[BulletinLigne] bl
    INNER JOIN [dbo].[Bulletin]        b   ON bl.[Bulletin] = b.[Oid]
    INNER JOIN [dbo].[Rubrique]        r   ON bl.[Rubrique] = r.[Oid]
    LEFT  JOIN [dbo].[RubriqueTypeRef] rtr ON r.[TypeRef]   = rtr.[Oid]
WHERE bl.[GCRecord] IS NULL
  AND b.[GCRecord]  IS NULL
  AND b.[Annee]    = @annee
GROUP BY rtr.[Code]
ORDER BY 1;


-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║  EXTERNE — ContratInterim                                                ║
-- ║  Coût = TauxJournalier × 22 jours × NbMois actifs dans l'année          ║
-- ╚══════════════════════════════════════════════════════════════════════════╝

-- 8. Coût total EXTERNE (recouvrement année)
WITH ContratsAnnee AS (
    SELECT
        ci.[Oid], ci.[Interimaire], ci.[Station], ci.[PosteOccupe], ci.[TauxJournalier],
        -- Période effective dans l'année [debut, fin]
        CASE WHEN ci.[DateDebut] < @debut THEN @debut ELSE ci.[DateDebut] END AS DateDebutEff,
        CASE WHEN ci.[DateFin] IS NULL OR ci.[DateFin] < '19000101' THEN
                CASE WHEN GETDATE() > @fin THEN @fin ELSE CAST(GETDATE() AS date) END
             WHEN ci.[DateFin] > @fin THEN @fin
             ELSE ci.[DateFin]
        END AS DateFinEff
    FROM [dbo].[ContratInterim] ci
    WHERE ci.[DateDebut] > '19000101'
      AND ci.[DateDebut] <= @fin
      AND (ci.[DateFin]  < '19000101' OR ci.[DateFin] >= @debut)
)
SELECT
    SUM(ca.[TauxJournalier] * 22 *
        ((YEAR(ca.DateFinEff) - YEAR(ca.DateDebutEff)) * 12
         + MONTH(ca.DateFinEff) - MONTH(ca.DateDebutEff) + 1)
    ) AS CoutTotalExterne,
    COUNT(DISTINCT ca.[Interimaire]) AS NbInterimDistincts,
    COUNT(*) AS NbContrats
FROM ContratsAnnee ca;

-- 9. Coût EXTERNE par STATION
WITH ContratsAnnee AS (
    SELECT
        ci.[Station], ci.[Interimaire], ci.[TauxJournalier],
        CASE WHEN ci.[DateDebut] < @debut THEN @debut ELSE ci.[DateDebut] END AS DateDebutEff,
        CASE WHEN ci.[DateFin] IS NULL OR ci.[DateFin] < '19000101' THEN
                CASE WHEN GETDATE() > @fin THEN @fin ELSE CAST(GETDATE() AS date) END
             WHEN ci.[DateFin] > @fin THEN @fin
             ELSE ci.[DateFin]
        END AS DateFinEff
    FROM [dbo].[ContratInterim] ci
    WHERE ci.[DateDebut] > '19000101' AND ci.[DateDebut] <= @fin
      AND (ci.[DateFin] < '19000101' OR ci.[DateFin] >= @debut)
)
SELECT
    ISNULL(st.[Nom], '(Non renseigné)') AS Station,
    SUM(ca.[TauxJournalier] * 22 *
        ((YEAR(ca.DateFinEff) - YEAR(ca.DateDebutEff)) * 12
         + MONTH(ca.DateFinEff) - MONTH(ca.DateDebutEff) + 1)
    ) AS CoutTotal,
    COUNT(DISTINCT ca.[Interimaire]) AS NbInterimaires
FROM ContratsAnnee ca
LEFT JOIN [dbo].[StationService] st ON st.[Oid] = ca.[Station]
GROUP BY st.[Nom]
ORDER BY CoutTotal DESC;

-- 10. Coût EXTERNE par POSTE
WITH ContratsAnnee AS (
    SELECT
        ci.[PosteOccupe], ci.[Interimaire], ci.[TauxJournalier],
        CASE WHEN ci.[DateDebut] < @debut THEN @debut ELSE ci.[DateDebut] END AS DateDebutEff,
        CASE WHEN ci.[DateFin] IS NULL OR ci.[DateFin] < '19000101' THEN
                CASE WHEN GETDATE() > @fin THEN @fin ELSE CAST(GETDATE() AS date) END
             WHEN ci.[DateFin] > @fin THEN @fin
             ELSE ci.[DateFin]
        END AS DateFinEff
    FROM [dbo].[ContratInterim] ci
    WHERE ci.[DateDebut] > '19000101' AND ci.[DateDebut] <= @fin
      AND (ci.[DateFin] < '19000101' OR ci.[DateFin] >= @debut)
)
SELECT
    ISNULL(p.[Libelle], '(Non renseigné)') AS Poste,
    SUM(ca.[TauxJournalier] * 22 *
        ((YEAR(ca.DateFinEff) - YEAR(ca.DateDebutEff)) * 12
         + MONTH(ca.DateFinEff) - MONTH(ca.DateDebutEff) + 1)
    ) AS CoutTotal,
    COUNT(DISTINCT ca.[Interimaire]) AS NbInterimaires
FROM ContratsAnnee ca
LEFT JOIN [dbo].[PosteInterimaire] p ON p.[Oid] = ca.[PosteOccupe]
GROUP BY p.[Libelle]
ORDER BY CoutTotal DESC;
