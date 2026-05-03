-- ============================================================================
-- 06_bilan_social.sql
-- Tableau N°6 — Bilan Social Mensuel (INTERNE)
--
-- Synthèse 12 mois × indicateurs principaux. Aligne avec
-- SPEC_PowerBI_DAX_to_SQL.sql mesure 9 (Récap Mensuel).
-- ============================================================================

DECLARE @annee int = 2025;

-- ── 1. Récap mensuel complet (12 lignes) ─────────────────────────────────
WITH MonthlyData AS (
    SELECT
        b.[Annee], b.[Mois],
        DATEFROMPARTS(b.[Annee], b.[Mois], 1)            AS DateMois,
        EOMONTH(DATEFROMPARTS(b.[Annee], b.[Mois], 1))   AS EOM,
        SUM(b.[BrutFiscal])         AS MasseBrute,
        COUNT(DISTINCT b.[Salarie]) AS NbBulletins
    FROM [dbo].[Bulletin] b
    WHERE b.[Annee]    = @annee
      AND b.[GCRecord] IS NULL
    GROUP BY b.[Annee], b.[Mois]
)
SELECT
    md.[Annee], md.[Mois],
    -- Effectif fin mois
    (SELECT COUNT(*) FROM [dbo].[Salarie] s
     WHERE s.[DateEmbauche] <= md.EOM
       AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101' OR s.[DateSortie] > md.EOM)
    ) AS EffectifFinMois,
    -- Embauches du mois
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateEmbauche]) = md.[Annee] AND MONTH([DateEmbauche]) = md.[Mois]
    ) AS EmbauchesMois,
    -- Sorties du mois
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE [DateSortie] >= '19000101'
       AND YEAR([DateSortie]) = md.[Annee] AND MONTH([DateSortie]) = md.[Mois]
    ) AS SortiesMois,
    md.MasseBrute,
    -- Charges patronales du mois
    ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
        WHERE b.[Annee] = md.[Annee] AND b.[Mois] = md.[Mois]
          AND b.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ), 0) AS ChargesPatronales,
    -- Coût employeur = masse + charges
    md.MasseBrute + ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
        WHERE b.[Annee] = md.[Annee] AND b.[Mois] = md.[Mois]
          AND b.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ), 0) AS CoutEmployeur,
    -- Employés absents du mois (CongeDemande Accordee recouvrant le mois)
    (SELECT COUNT(DISTINCT c.[Salarie])
     FROM [dbo].[CongeDemande] c
     WHERE c.[Statut] = 20
       AND c.[DateDebut] <= md.EOM
       AND c.[DateFin]   >= md.DateMois
    ) AS EmployesAbsents,
    -- Total jours d'absence dans le mois (somme DureeJours, prorata simplifié)
    ISNULL((
        SELECT SUM(c.[DureeJours])
        FROM [dbo].[CongeDemande] c
        WHERE c.[Statut] = 20
          AND YEAR(c.[DateDebut]) = md.[Annee]
          AND MONTH(c.[DateDebut]) = md.[Mois]
    ), 0) AS JoursAbsence
FROM MonthlyData md
ORDER BY md.[Mois];

-- ── 2. Ligne TOTAL (synthèse annuelle) ───────────────────────────────────
SELECT
    @annee AS Annee,
    -- Effectif au 31/12
    (SELECT COUNT(*) FROM [dbo].[Salarie] s
     WHERE s.[DateEmbauche] <= DATEFROMPARTS(@annee, 12, 31)
       AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101'
            OR s.[DateSortie] > DATEFROMPARTS(@annee, 12, 31))
    ) AS EffectifFin,
    -- Total embauches année
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateEmbauche]) = @annee) AS TotalEmbauches,
    -- Total départs année
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateSortie]) = @annee
       AND [DateSortie] >= '19000101') AS TotalDeparts,
    -- Masse brute annuelle
    (SELECT SUM(b.[BrutFiscal]) FROM [dbo].[Bulletin] b
     WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL) AS MasseAnnuelle,
    -- Charges patronales annuelles
    (SELECT SUM(bl.[MontantEmployeur])
     FROM [dbo].[BulletinLigne] bl
     INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
     WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ) AS ChargesAnnuelles,
    -- Total jours d'absence année
    (SELECT SUM(c.[DureeJours]) FROM [dbo].[CongeDemande] c
     WHERE c.[Statut] = 20 AND YEAR(c.[DateDebut]) = @annee
    ) AS TotalJoursAbsence;

-- ── 3. Taux d'absentéisme annuel = jours / (effectif × 264) ──────────────
WITH Tot AS (
    SELECT ISNULL(SUM(c.[DureeJours]), 0) AS Jours
    FROM [dbo].[CongeDemande] c
    WHERE c.[Statut] = 20 AND YEAR(c.[DateDebut]) = @annee
),
Eff AS (
    SELECT COUNT(*) AS N FROM [dbo].[Salarie] s
    WHERE s.[DateEmbauche] <= DATEFROMPARTS(@annee, 12, 31)
      AND (s.[DateSortie] IS NULL OR s.[DateSortie] < '19000101'
           OR s.[DateSortie] > DATEFROMPARTS(@annee, 12, 31))
)
SELECT
    (SELECT Jours FROM Tot) AS TotalJoursAbsence,
    (SELECT N FROM Eff)     AS EffectifFin,
    CAST(
        (SELECT Jours FROM Tot) * 100.0 /
        NULLIF((SELECT N FROM Eff) * 264.0, 0)
    AS DECIMAL(6, 2)) AS TauxAbsenteisme_Pct;
