-- ╔══════════════════════════════════════════════════════════════════════════╗
-- ║                                                                          ║
-- ║   install.sql — Tableaux de Bord RH SunuPaie / ELTON Oil Company         ║
-- ║                                                                          ║
-- ║   Script consolidé (6 modules) — équivalent SQL des KPI calculés en      ║
-- ║   C#/XPO par les services dashboards.                                    ║
-- ║                                                                          ║
-- ║   ⚠️  Ce script N'EST PAS exécuté par l'application Blazor.              ║
-- ║      Usages prévus :                                                      ║
-- ║       - Audit DBA (vérifier la parité C# ↔ SQL)                          ║
-- ║       - Plan B en cas d'app down                                          ║
-- ║       - Connexion directe Power BI / Excel                               ║
-- ║       - Documentation métier rapide                                      ║
-- ║       - Tests d'intégration                                               ║
-- ║                                                                          ║
-- ║   Version    : 1.1 (mai 2026)                                            ║
-- ║   Auteur     : Mission « Tableaux de Bord RH »                           ║
-- ║   Plateforme : Microsoft SQL Server 2019+                                ║
-- ║                                                                          ║
-- ║   ─── V1.1 — Refonte Module Intérimaire ────────────────────────────     ║
-- ║   Le modèle EXTERNE a été refondu (mai 2026) :                           ║
-- ║     - StationService (legacy)            → Site (TypeSite enum)          ║
-- ║         Type 0=StationService 🏪 / 1=Siege 🏢 / 2=Depot 📦 / 3=Autre 📍  ║
-- ║     - BusinessUnitStation (legacy)       → UniteOrganisationnelle        ║
-- ║         (récursive Parent/Enfants, TypeUnite : BU/Departement/Segment)   ║
-- ║     - ContratInterim.Station (legacy)    → ContratInterim.Site (V1.1)    ║
-- ║     - ContratInterim.BU      (legacy)    → ContratInterim.Unites (N-N)   ║
-- ║   Les colonnes legacy restent en base (compat) mais ne sont plus lues.   ║
-- ║                                                                          ║
-- ╚══════════════════════════════════════════════════════════════════════════╝
--
-- ════════════════════════════════════════════════════════════════════════════
--   TABLE DES MATIÈRES
-- ════════════════════════════════════════════════════════════════════════════
--   §0    PARAMÈTRES GLOBAUX                                          ligne ~50
--
--   §1    TABLEAU N°1 — Effectif détaillé                             ligne ~70
--          1.1  Effectif total                                              KPI
--          1.2  Effectif fin mois (variant)                                 KPI
--          1.3  Démographie (âge moy, ancienneté moy, %F/%H)               KPI
--          1.4  Pyramide des âges × catégorie                             chart
--          1.5  Bar stack F/H par tranche × catégorie                     chart
--
--   §2    TABLEAU N°2 — Analyse de l'Effectif                         ligne ~140
--          2.1  Effectif global / moyen / évolution 8 ans                  KPI
--          2.2  Turnover %                                                  KPI
--          2.3  Bar charts (tranche, ancienneté, segment, cat, contrat) chart
--
--   §3    TABLEAU N°3 — Mouvements (Arrivées / Départs)               ligne ~270
--          3.1  INTERNE : Salarié.DateEmbauche / DateSortie + Motif
--          3.2  EXTERNE : ContratInterim DateDebut / DateFinReelle
--          3.3  EXTERNE V1.1 : Arrivées par Site (TypeSite enum + emoji)
--
--   §4    TABLEAU N°4 — Rémunération (Égalité H/F)                    ligne ~430
--          4.1  Masse Brute, Coût Employeur, Net total
--          4.2  KPI Min / Max / Moyen par bulletin
--          4.3  Égalité par segment (Département)
--          4.4  EXTERNE : Coût total (TauxJournalier × 22j × NbMois)
--          4.5  EXTERNE V1.1 : Coût total par Site (TypeSite enum + emoji)
--          (cf. fichier détaillé : sql/dashboards/04_remuneration.sql — 11 requêtes)
--
--   §5    TABLEAU N°5 — Suivi des Absences (INTERNE)                  ligne ~570
--          5.1  KPI globaux + Taux d'absentéisme
--          5.2  Par famille de congé / catégorie / département
--          5.3  Top 10 salariés les plus absents
--
--   §6    TABLEAU N°6 — Bilan Social Mensuel (INTERNE)                ligne ~700
--          6.1  Récap 12 mois × 8 indicateurs (Effectif, Embauches, Départs,
--               Masse, Charges, Coût Employeur, Employés Absents, Jours Abs.)
--          6.2  Synthèse annuelle
--          6.3  Taux d'absentéisme annuel
--
-- ════════════════════════════════════════════════════════════════════════════

SET DATEFORMAT ymd;

-- ════════════════════════════════════════════════════════════════════════════
-- §0  PARAMÈTRES GLOBAUX
-- ════════════════════════════════════════════════════════════════════════════
DECLARE @annee   int  = 2025;
DECLARE @debut   date = DATEFROMPARTS(@annee,  1,  1);
DECLARE @fin     date = DATEFROMPARTS(@annee, 12, 31);
DECLARE @dateRef date = CASE WHEN @annee = YEAR(GETDATE())
                             THEN CAST(GETDATE() AS date)
                             ELSE @fin END;
-- Sentinelle : DateSortie >= '19000101' → traite DateTime.MinValue + epoch Excel + NULL
DECLARE @sentinelle date = '1900-01-01';

-- ════════════════════════════════════════════════════════════════════════════
-- §1  TABLEAU N°1 — Effectif détaillé (cf. sql/dashboards/01_effectif_detaille.sql)
-- ════════════════════════════════════════════════════════════════════════════

-- 1.1 Effectif total au @dateRef
SELECT COUNT(DISTINCT s.[Oid]) AS Effectif_Total
FROM [dbo].[Salarie] s
WHERE (s.[DateEmbauche] IS NULL OR s.[DateEmbauche] <= @dateRef)
  AND (s.[DateSortie]   IS NULL
       OR s.[DateSortie] < @sentinelle
       OR s.[DateSortie] > @dateRef);

-- 1.2 KPI démographiques (Âge / Ancienneté moyens, %F/%H)
SELECT
    AVG(DATEDIFF(YEAR, p.[Birthday], @dateRef))      AS AgeMoyen,
    AVG(DATEDIFF(YEAR, s.[DateEmbauche], @dateRef))  AS AncienneteMoyenne,
    SUM(CASE WHEN s.[Sexe] = 0 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS PctHommes,
    SUM(CASE WHEN s.[Sexe] = 1 THEN 1 ELSE 0 END) * 100.0 / NULLIF(COUNT(*), 0) AS PctFemmes
FROM [dbo].[Salarie] s
    INNER JOIN [dbo].[Person] p ON s.[Oid] = p.[Oid]
WHERE s.[DateSortie] IS NULL OR s.[DateSortie] < @sentinelle OR s.[DateSortie] > @dateRef;

-- 1.3 Pyramide des âges × Sexe
SELECT
    CASE
        WHEN p.[Birthday] IS NULL OR p.[Birthday] < @sentinelle THEN N'(non défini)'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 25 THEN N'< 25 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 35 THEN N'25 - 34 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 45 THEN N'35 - 44 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 55 THEN N'45 - 54 ans'
        ELSE                                                 N'55 ans et plus'
    END AS TrancheAge,
    SUM(CASE WHEN s.[Sexe] = 0 THEN 1 ELSE 0 END) AS Hommes,
    SUM(CASE WHEN s.[Sexe] = 1 THEN 1 ELSE 0 END) AS Femmes,
    COUNT(*) AS Total
FROM [dbo].[Salarie] s
    INNER JOIN [dbo].[Person] p ON s.[Oid] = p.[Oid]
WHERE s.[DateSortie] IS NULL OR s.[DateSortie] < @sentinelle OR s.[DateSortie] > @dateRef
GROUP BY
    CASE
        WHEN p.[Birthday] IS NULL OR p.[Birthday] < @sentinelle THEN N'(non défini)'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 25 THEN N'< 25 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 35 THEN N'25 - 34 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 45 THEN N'35 - 44 ans'
        WHEN DATEDIFF(YEAR, p.[Birthday], @dateRef) < 55 THEN N'45 - 54 ans'
        ELSE                                                 N'55 ans et plus'
    END
ORDER BY MIN(DATEDIFF(YEAR, p.[Birthday], @dateRef));

-- ════════════════════════════════════════════════════════════════════════════
-- §2  TABLEAU N°2 — Analyse de l'Effectif (cf. 02_analyse_effectif.sql)
-- ════════════════════════════════════════════════════════════════════════════

-- 2.1 Effectif moyen annuel = (effectif au 1/1 + effectif au 31/12) / 2
SELECT
    ((SELECT COUNT(*) FROM [dbo].[Salarie]
      WHERE [DateEmbauche] <= @debut
        AND ([DateSortie] IS NULL OR [DateSortie] < @sentinelle OR [DateSortie] > @debut))
     +
     (SELECT COUNT(*) FROM [dbo].[Salarie]
      WHERE [DateEmbauche] <= @fin
        AND ([DateSortie] IS NULL OR [DateSortie] < @sentinelle OR [DateSortie] > @fin))
    ) / 2.0 AS EffectifMoyen;

-- 2.2 Turnover % annuel = Sorties / Effectif moyen
WITH Sorties AS (
    SELECT COUNT(*) AS V FROM [dbo].[Salarie]
    WHERE [DateSortie] >= @sentinelle AND YEAR([DateSortie]) = @annee
), EffMoy AS (
    SELECT (
        (SELECT COUNT(*) FROM [dbo].[Salarie]
         WHERE [DateEmbauche] <= @debut
           AND ([DateSortie] IS NULL OR [DateSortie] < @sentinelle OR [DateSortie] > @debut))
        +
        (SELECT COUNT(*) FROM [dbo].[Salarie]
         WHERE [DateEmbauche] <= @fin
           AND ([DateSortie] IS NULL OR [DateSortie] < @sentinelle OR [DateSortie] > @fin))
    ) / 2.0 AS V
)
SELECT
    (SELECT V FROM Sorties) AS Sorties,
    (SELECT V FROM EffMoy)  AS EffectifMoyen,
    CAST((SELECT V FROM Sorties) * 100.0 / NULLIF((SELECT V FROM EffMoy), 0) AS DECIMAL(5,2)) AS Turnover_Pct;

-- ════════════════════════════════════════════════════════════════════════════
-- §3  TABLEAU N°3 — Mouvements (cf. 03_mouvements.sql)
-- ════════════════════════════════════════════════════════════════════════════

-- 3.1 INTERNE : Embauches + Départs avec motif
SELECT COUNT(*) AS NbArrivees
FROM [dbo].[Salarie]
WHERE [DateEmbauche] >= @debut AND [DateEmbauche] <= @fin;

SELECT
    CASE [MotifDepart]
        WHEN 0 THEN N'Démission'
        WHEN 1 THEN N'Licenciement'
        WHEN 2 THEN N'Retraite'
        WHEN 3 THEN N'Fin de CDD'
        WHEN 4 THEN N'Rupture conventionnelle'
        WHEN 5 THEN N'Décès'
        WHEN 6 THEN N'Autre'
        ELSE        N'(Non renseigné)'
    END AS Motif,
    COUNT(*) AS NbDeparts
FROM [dbo].[Salarie]
WHERE [DateSortie] >= @sentinelle AND YEAR([DateSortie]) = @annee
GROUP BY [MotifDepart]
ORDER BY NbDeparts DESC;

-- 3.2 EXTERNE : nouveaux contrats + clos (Statut Termine=2 ou Resilie=3)
SELECT COUNT(*) AS NbArriveesExt
FROM [dbo].[ContratInterim]
WHERE [DateDebut] >= @debut AND [DateDebut] <= @fin;

SELECT COUNT(*) AS NbDepartsExt
FROM [dbo].[ContratInterim]
WHERE [Statut] IN (2, 3)
  AND COALESCE([DateFinReelle], [DateFin]) BETWEEN @debut AND @fin;

-- 3.3 EXTERNE V1.1 : Arrivées par SITE (TypeSite enum + emoji)
--     Remplace l'ancien GROUP BY [StationService] (legacy) par GROUP BY Site V1.1.
SELECT
    ISNULL(si.[Nom], '(Non renseigné)')              AS SiteNom,
    CASE si.[Type]
        WHEN 0 THEN N'🏪 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 1 THEN N'🏢 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 2 THEN N'📦 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 3 THEN N'📍 ' + ISNULL(si.[Nom], '(Non renseigné)')
        ELSE        ISNULL(si.[Nom], '(Non renseigné)')
    END                                              AS SiteNomAvecType,
    si.[Type]                                        AS TypeSiteCode,
    COUNT(*)                                         AS NbArrivees
FROM [dbo].[ContratInterim] ci
LEFT JOIN [dbo].[Site] si ON si.[Oid] = ci.[Site]
WHERE ci.[DateDebut] >= @debut AND ci.[DateDebut] <= @fin
GROUP BY si.[Nom], si.[Type]
ORDER BY NbArrivees DESC;

-- ════════════════════════════════════════════════════════════════════════════
-- §4  TABLEAU N°4 — Rémunération (cf. 04_remuneration.sql)
-- ════════════════════════════════════════════════════════════════════════════

-- 4.1 Coût Employeur = Masse Brute + Charges Patronales
SELECT
    SUM(b.[BrutFiscal]) AS MasseBrute,
    ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b2 ON bl.[Bulletin] = b2.[Oid]
        WHERE b2.[Annee] = @annee AND b2.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ), 0) AS ChargesPatronales,
    SUM(b.[BrutFiscal]) + ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b2 ON bl.[Bulletin] = b2.[Oid]
        WHERE b2.[Annee] = @annee AND b2.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ), 0) AS CoutEmployeur,
    SUM(b.[NetAPayer]) AS NetTotal
FROM [dbo].[Bulletin] b
WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL;

-- 4.2 Égalité H/F par catégorie professionnelle
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
WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL
GROUP BY cat.[Intitule]
ORDER BY Total DESC;

-- 4.3 Décomposition par 7 familles macro de rubrique
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
    SUM(bl.[Montant])                                    AS TotalSalarial,
    SUM(bl.[MontantEmployeur])                           AS TotalEmployeur,
    SUM(bl.[Montant] + ISNULL(bl.[MontantEmployeur], 0)) AS TotalCombine
FROM [dbo].[BulletinLigne] bl
    INNER JOIN [dbo].[Bulletin]        b   ON bl.[Bulletin] = b.[Oid]
    INNER JOIN [dbo].[Rubrique]        r   ON bl.[Rubrique] = r.[Oid]
    LEFT  JOIN [dbo].[RubriqueTypeRef] rtr ON r.[TypeRef]   = rtr.[Oid]
WHERE bl.[GCRecord] IS NULL AND b.[GCRecord] IS NULL AND b.[Annee] = @annee
GROUP BY rtr.[Code]
ORDER BY 1;

-- 4.4 EXTERNE : Coût total (formule TauxJournalier × 22 × NbMois actifs)
WITH ContratsAnnee AS (
    SELECT
        ci.[Site], ci.[Interimaire], ci.[TauxJournalier],
        CASE WHEN ci.[DateDebut] < @debut THEN @debut ELSE ci.[DateDebut] END AS DateDebutEff,
        CASE WHEN ci.[DateFin] IS NULL OR ci.[DateFin] < @sentinelle THEN
                CASE WHEN GETDATE() > @fin THEN @fin ELSE CAST(GETDATE() AS date) END
             WHEN ci.[DateFin] > @fin THEN @fin
             ELSE ci.[DateFin]
        END AS DateFinEff
    FROM [dbo].[ContratInterim] ci
    WHERE ci.[DateDebut] > @sentinelle AND ci.[DateDebut] <= @fin
      AND (ci.[DateFin] < @sentinelle OR ci.[DateFin] >= @debut)
)
SELECT SUM(
    ca.[TauxJournalier] * 22 *
    ((YEAR(ca.DateFinEff) - YEAR(ca.DateDebutEff)) * 12
     + MONTH(ca.DateFinEff) - MONTH(ca.DateDebutEff) + 1)
) AS CoutTotalExterne
FROM ContratsAnnee ca;

-- 4.5 EXTERNE V1.1 : Coût total par SITE (TypeSite enum + emoji)
--     Remplace l'ancien GROUP BY [StationService] (legacy) par GROUP BY Site V1.1.
WITH ContratsAnnee AS (
    SELECT
        ci.[Site], ci.[Interimaire], ci.[TauxJournalier],
        CASE WHEN ci.[DateDebut] < @debut THEN @debut ELSE ci.[DateDebut] END AS DateDebutEff,
        CASE WHEN ci.[DateFin] IS NULL OR ci.[DateFin] < @sentinelle THEN
                CASE WHEN GETDATE() > @fin THEN @fin ELSE CAST(GETDATE() AS date) END
             WHEN ci.[DateFin] > @fin THEN @fin
             ELSE ci.[DateFin]
        END AS DateFinEff
    FROM [dbo].[ContratInterim] ci
    WHERE ci.[DateDebut] > @sentinelle AND ci.[DateDebut] <= @fin
      AND (ci.[DateFin] < @sentinelle OR ci.[DateFin] >= @debut)
)
SELECT
    ISNULL(si.[Nom], '(Non renseigné)') AS SiteNom,
    CASE si.[Type]
        WHEN 0 THEN N'🏪 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 1 THEN N'🏢 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 2 THEN N'📦 ' + ISNULL(si.[Nom], '(Non renseigné)')
        WHEN 3 THEN N'📍 ' + ISNULL(si.[Nom], '(Non renseigné)')
        ELSE        ISNULL(si.[Nom], '(Non renseigné)')
    END                                 AS SiteNomAvecType,
    si.[Type]                           AS TypeSiteCode,
    SUM(ca.[TauxJournalier] * 22 *
        ((YEAR(ca.DateFinEff) - YEAR(ca.DateDebutEff)) * 12
         + MONTH(ca.DateFinEff) - MONTH(ca.DateDebutEff) + 1)
    )                                   AS CoutTotal,
    COUNT(DISTINCT ca.[Interimaire])    AS NbInterimaires
FROM ContratsAnnee ca
LEFT JOIN [dbo].[Site] si ON si.[Oid] = ca.[Site]
GROUP BY si.[Nom], si.[Type]
ORDER BY CoutTotal DESC;

-- ════════════════════════════════════════════════════════════════════════════
-- §5  TABLEAU N°5 — Suivi des Absences (cf. 05_suivi_absences.sql)
-- ════════════════════════════════════════════════════════════════════════════

-- 5.1 KPI globaux + taux d'absentéisme (base 264 j ouvrables/an)
WITH Tot AS (
    SELECT ISNULL(SUM(c.[DureeJours]), 0) AS Jours,
           COUNT(*)                       AS NbAbs,
           COUNT(DISTINCT c.[Salarie])    AS NbSalAbs
    FROM [dbo].[CongeDemande] c
    WHERE c.[Statut] = 20    -- Accordee
      AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
), Eff AS (
    SELECT COUNT(*) AS N FROM [dbo].[Salarie]
    WHERE [DateEmbauche] <= @fin
      AND ([DateSortie] IS NULL OR [DateSortie] < @sentinelle OR [DateSortie] > @fin)
)
SELECT
    (SELECT NbAbs FROM Tot)      AS NbAbsences,
    (SELECT Jours FROM Tot)      AS TotalJours,
    (SELECT NbSalAbs FROM Tot)   AS NbSalariesAbsents,
    (SELECT N FROM Eff)          AS EffectifFin,
    CAST(
        (SELECT Jours FROM Tot) * 100.0 / NULLIF((SELECT N FROM Eff) * 264.0, 0)
    AS DECIMAL(6,2)) AS TauxAbsenteisme_Pct;

-- 5.2 Répartition par famille de congé
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
WHERE c.[Statut] = 20 AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
GROUP BY ct.[Famille]
ORDER BY TotalJours DESC;

-- 5.3 Top 10 salariés les plus absents
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
WHERE c.[Statut] = 20 AND c.[DateDebut] <= @fin AND c.[DateFin] >= @debut
GROUP BY p.[FirstName], p.[LastName], cat.[Intitule], d.[Nom]
ORDER BY TotalJours DESC;

-- ════════════════════════════════════════════════════════════════════════════
-- §6  TABLEAU N°6 — Bilan Social Mensuel (cf. 06_bilan_social.sql)
-- ════════════════════════════════════════════════════════════════════════════

-- 6.1 Récap mensuel 12 lignes × 8 indicateurs principaux
WITH MonthlyData AS (
    SELECT
        b.[Annee], b.[Mois],
        DATEFROMPARTS(b.[Annee], b.[Mois], 1)            AS DateMois,
        EOMONTH(DATEFROMPARTS(b.[Annee], b.[Mois], 1))   AS EOM,
        SUM(b.[BrutFiscal])         AS MasseBrute
    FROM [dbo].[Bulletin] b
    WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL
    GROUP BY b.[Annee], b.[Mois]
)
SELECT
    md.[Mois],
    -- Effectif fin mois
    (SELECT COUNT(*) FROM [dbo].[Salarie] s
     WHERE s.[DateEmbauche] <= md.EOM
       AND (s.[DateSortie] IS NULL OR s.[DateSortie] < @sentinelle OR s.[DateSortie] > md.EOM)
    ) AS EffectifFinMois,
    -- Embauches du mois
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateEmbauche]) = md.[Annee] AND MONTH([DateEmbauche]) = md.[Mois]
    ) AS Embauches,
    -- Sorties du mois
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE [DateSortie] >= @sentinelle
       AND YEAR([DateSortie]) = md.[Annee] AND MONTH([DateSortie]) = md.[Mois]
    ) AS Departs,
    md.MasseBrute,
    ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
        WHERE b.[Annee] = md.[Annee] AND b.[Mois] = md.[Mois]
          AND b.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ), 0) AS ChargesPatronales,
    md.MasseBrute + ISNULL((
        SELECT SUM(bl.[MontantEmployeur])
        FROM [dbo].[BulletinLigne] bl
        INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
        WHERE b.[Annee] = md.[Annee] AND b.[Mois] = md.[Mois]
          AND b.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ), 0) AS CoutEmployeur,
    (SELECT COUNT(DISTINCT c.[Salarie])
     FROM [dbo].[CongeDemande] c
     WHERE c.[Statut] = 20
       AND c.[DateDebut] <= md.EOM
       AND c.[DateFin]   >= md.DateMois
    ) AS EmployesAbsents,
    ISNULL((
        SELECT SUM(c.[DureeJours])
        FROM [dbo].[CongeDemande] c
        WHERE c.[Statut] = 20
          AND YEAR(c.[DateDebut]) = md.[Annee] AND MONTH(c.[DateDebut]) = md.[Mois]
    ), 0) AS JoursAbsence
FROM MonthlyData md
ORDER BY md.[Mois];

-- 6.2 Synthèse annuelle
SELECT
    @annee AS Annee,
    (SELECT COUNT(*) FROM [dbo].[Salarie] s
     WHERE s.[DateEmbauche] <= @fin
       AND (s.[DateSortie] IS NULL OR s.[DateSortie] < @sentinelle OR s.[DateSortie] > @fin)
    ) AS EffectifFin,
    (SELECT COUNT(*) FROM [dbo].[Salarie] WHERE YEAR([DateEmbauche]) = @annee) AS TotalEmbauches,
    (SELECT COUNT(*) FROM [dbo].[Salarie]
     WHERE YEAR([DateSortie]) = @annee AND [DateSortie] >= @sentinelle) AS TotalDeparts,
    (SELECT SUM(b.[BrutFiscal]) FROM [dbo].[Bulletin] b
     WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL) AS MasseAnnuelle,
    (SELECT SUM(bl.[MontantEmployeur])
     FROM [dbo].[BulletinLigne] bl
     INNER JOIN [dbo].[Bulletin] b ON bl.[Bulletin] = b.[Oid]
     WHERE b.[Annee] = @annee AND b.[GCRecord] IS NULL AND bl.[GCRecord] IS NULL
    ) AS ChargesAnnuelles,
    (SELECT SUM(c.[DureeJours]) FROM [dbo].[CongeDemande] c
     WHERE c.[Statut] = 20 AND YEAR(c.[DateDebut]) = @annee
    ) AS TotalJoursAbsence;

-- ════════════════════════════════════════════════════════════════════════════
--  FIN DU SCRIPT — install.sql v1.0
--
--  Pour exécuter avec une autre année :
--      Modifier `DECLARE @annee int = 2025;` en haut du script (§0).
--
--  Pour exécuter section par section :
--      Sélectionner uniquement le bloc §X souhaité dans SSMS / Azure Data Studio.
--
--  Les fichiers individuels (01_*.sql à 06_*.sql) restent disponibles dans
--  sql/dashboards/ pour aller plus loin (toutes les requêtes par module).
-- ════════════════════════════════════════════════════════════════════════════
