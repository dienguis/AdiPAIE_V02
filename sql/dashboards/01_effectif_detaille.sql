-- ============================================================================
-- 01_effectif_detaille.sql
-- Tableau N°1 — Effectif détaillé (Personnel INTERNE)
--
-- Ces requêtes sont l'équivalent SQL Server de ce que fait
-- EffectifDetailleDashboardService côté C#/XPO. Elles servent :
--   1) De documentation pour audit / data team / DTSS.
--   2) De source pour profiler les données et calibrer les performances.
--   3) De base si on doit basculer un calcul depuis XPO vers SQL natif.
--
-- Hypothèses :
--   - "Salarie".DateSortie = '0001-01-01' (DateTime.MinValue) ou > @dateRef
--     pour les actifs.
--   - "Salarie".Birthday est la date de naissance (Person de XAF).
--   - "Categories".Intitule est l'axe « catégorie professionnelle ».
--   - "ContratSalarie".TypeContrat enum 0=CDI, 1=CDD, 2=Stage.
-- ============================================================================

DECLARE @dateRef    DATETIME = GETDATE();   -- Date de référence
DECLARE @siteOid    UNIQUEIDENTIFIER = NULL;-- NULL = tous les sites
DECLARE @sexe       INT = NULL;             -- 0=Masculin, 1=Feminin, NULL=tous
DECLARE @typContrat INT = NULL;             -- 0=CDI, 1=CDD, 2=Stage, NULL=tous

-- ----------------------------------------------------------------------------
-- 1) Salariés actifs à la date de référence (avant filtre TypeContrat)
-- ----------------------------------------------------------------------------
WITH ActifsBase AS (
    SELECT s.Oid, s.Birthday, s.DateEmbauche, s.DateSortie,
           s.Sexe, s.Categories, s.Site
    FROM   "Salarie" s
    WHERE  s.DateEmbauche <= @dateRef
      AND  (s.DateSortie = '0001-01-01' OR s.DateSortie > @dateRef)
      AND  (@siteOid IS NULL OR s.Site = @siteOid)
      AND  (@sexe    IS NULL OR s.Sexe = @sexe)
)
-- ... CTE utilisée par les requêtes ci-dessous

-- ----------------------------------------------------------------------------
-- 2) KPI : Âge moyen et Ancienneté moyenne (global / hommes / femmes)
-- ----------------------------------------------------------------------------
SELECT
    -- Âge moyen
    AVG(DATEDIFF(year, Birthday, @dateRef))    AS AgeMoyenGlobal,
    AVG(CASE WHEN Sexe = 0
             THEN DATEDIFF(year, Birthday, @dateRef) END) AS AgeMoyenHommes,
    AVG(CASE WHEN Sexe = 1
             THEN DATEDIFF(year, Birthday, @dateRef) END) AS AgeMoyenFemmes,
    -- Ancienneté moyenne
    AVG(DATEDIFF(year, DateEmbauche, @dateRef))            AS AncMoyGlobal,
    AVG(CASE WHEN Sexe = 0
             THEN DATEDIFF(year, DateEmbauche, @dateRef) END) AS AncMoyHommes,
    AVG(CASE WHEN Sexe = 1
             THEN DATEDIFF(year, DateEmbauche, @dateRef) END) AS AncMoyFemmes
FROM ActifsBase;

-- ----------------------------------------------------------------------------
-- 3) Évolution effectif sur 3 ans
--    - Année courante : photo au @dateRef.
--    - Années passées : photo au 31/12.
-- ----------------------------------------------------------------------------
;WITH Annees AS (
    SELECT YEAR(@dateRef) - 2 AS Annee
    UNION SELECT YEAR(@dateRef) - 1
    UNION SELECT YEAR(@dateRef)
)
SELECT a.Annee,
       (SELECT COUNT(*) FROM "Salarie" s
        WHERE s.DateEmbauche <= CASE
                                  WHEN a.Annee = YEAR(@dateRef) THEN @dateRef
                                  ELSE DATEFROMPARTS(a.Annee, 12, 31)
                                END
          AND (s.DateSortie = '0001-01-01' OR s.DateSortie > CASE
                                                              WHEN a.Annee = YEAR(@dateRef) THEN @dateRef
                                                              ELSE DATEFROMPARTS(a.Annee, 12, 31)
                                                            END)
       ) AS Effectif
FROM Annees a
ORDER BY a.Annee;

-- ----------------------------------------------------------------------------
-- 4) Effectif par tranche d'âge × catégorie professionnelle
-- ----------------------------------------------------------------------------
;WITH ActifsAvecAge AS (
    SELECT s.Oid, s.Categories,
           DATEDIFF(year, s.Birthday, @dateRef) AS Age
    FROM   "Salarie" s
    WHERE  s.DateEmbauche <= @dateRef
      AND  (s.DateSortie = '0001-01-01' OR s.DateSortie > @dateRef)
)
SELECT CASE
         WHEN Age < 25 THEN '<25'
         WHEN Age BETWEEN 25 AND 34 THEN '25-34'
         WHEN Age BETWEEN 35 AND 44 THEN '35-44'
         WHEN Age BETWEEN 45 AND 54 THEN '45-54'
         ELSE '55+'
       END                                AS Tranche,
       c.Intitule                         AS Categorie,
       COUNT(*)                           AS Effectif
FROM   ActifsAvecAge a
JOIN   "Categories" c ON c.Oid = a.Categories
GROUP BY
       CASE
         WHEN Age < 25 THEN '<25'
         WHEN Age BETWEEN 25 AND 34 THEN '25-34'
         WHEN Age BETWEEN 35 AND 44 THEN '35-44'
         WHEN Age BETWEEN 45 AND 54 THEN '45-54'
         ELSE '55+'
       END,
       c.Intitule
ORDER BY Tranche, Categorie;

-- ----------------------------------------------------------------------------
-- 5) Bar chart F/H par tranche × catégorie (count par sexe)
-- ----------------------------------------------------------------------------
;WITH ActifsAvecAge AS (
    SELECT s.Oid, s.Categories, s.Sexe,
           DATEDIFF(year, s.Birthday, @dateRef) AS Age
    FROM   "Salarie" s
    WHERE  s.DateEmbauche <= @dateRef
      AND  (s.DateSortie = '0001-01-01' OR s.DateSortie > @dateRef)
)
SELECT CASE
         WHEN Age < 25 THEN '<25'
         WHEN Age BETWEEN 25 AND 34 THEN '25-34'
         WHEN Age BETWEEN 35 AND 44 THEN '35-44'
         WHEN Age BETWEEN 45 AND 54 THEN '45-54'
         ELSE '55+'
       END                                              AS Tranche,
       c.Intitule                                       AS Categorie,
       SUM(CASE WHEN a.Sexe = 0 THEN 1 ELSE 0 END)     AS Hommes,
       SUM(CASE WHEN a.Sexe = 1 THEN 1 ELSE 0 END)     AS Femmes
FROM   ActifsAvecAge a
JOIN   "Categories" c ON c.Oid = a.Categories
GROUP BY
       CASE
         WHEN Age < 25 THEN '<25'
         WHEN Age BETWEEN 25 AND 34 THEN '25-34'
         WHEN Age BETWEEN 35 AND 44 THEN '35-44'
         WHEN Age BETWEEN 45 AND 54 THEN '45-54'
         ELSE '55+'
       END,
       c.Intitule
ORDER BY Tranche, Categorie;

-- ----------------------------------------------------------------------------
-- Notes :
--   - Le calcul d'âge via DATEDIFF(year, ...) en SQL Server peut différer
--     d'une année par rapport à un calcul jour-précis (anniversaires non
--     encore passés à @dateRef). Le service C# utilise une comparaison
--     précise via Birthday.AddYears(age) — privilégier le résultat C#.
--   - Pour le filtre TypeContrat, joindre "ContratSalarie" sur Salarie
--     avec WHERE TypeContrat = @typContrat ET DateDebut <= @dateRef ET
--     (DateFin IS NULL OR DateFin >= @dateRef).
-- ============================================================================
