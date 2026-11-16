-- ============================================================================
-- V1.12 - Fix ordre d'affichage des sections dans le DetailView Bulletin
-- ----------------------------------------------------------------------------
-- Force les 4 OrdreGroupe aux valeurs reglementaires du bulletin PDF.
-- Idempotent : relancer plusieurs fois est sans effet.
--
-- V1.12.1 - Retire la reference a NormalizedKey (colonne absente sur
-- certains environnements), on identifie par Libelle LIKE uniquement.
-- ============================================================================

-- Verification avant
PRINT '=== AVANT fix ===';
SELECT Code, Libelle, OrdreGroupe, Actif
FROM GroupeImpressionRef
WHERE Libelle LIKE N'Salaire brut%'
   OR Libelle LIKE N'Total Cotisations Sociales%'
   OR Libelle LIKE N'Total Retenues Fiscales%'
   OR Libelle LIKE N'Total Autres Retenues%'
ORDER BY Libelle;

-- Fix : 4 UPDATE par Libelle

-- 1. Salaire brut -> ordre 1
UPDATE GroupeImpressionRef
SET OrdreGroupe = 1
WHERE Libelle LIKE N'Salaire brut%'
  AND (OrdreGroupe IS NULL OR OrdreGroupe <> 1);

-- 2. Cotisations Sociales -> ordre 2
UPDATE GroupeImpressionRef
SET OrdreGroupe = 2
WHERE Libelle LIKE N'Total Cotisations Sociales%'
  AND (OrdreGroupe IS NULL OR OrdreGroupe <> 2);

-- 3. Retenues Fiscales -> ordre 3
UPDATE GroupeImpressionRef
SET OrdreGroupe = 3
WHERE Libelle LIKE N'Total Retenues Fiscales%'
  AND (OrdreGroupe IS NULL OR OrdreGroupe <> 3);

-- 4. Autres Retenues -> ordre 4
UPDATE GroupeImpressionRef
SET OrdreGroupe = 4
WHERE Libelle LIKE N'Total Autres Retenues%'
  AND (OrdreGroupe IS NULL OR OrdreGroupe <> 4);

-- Verification apres
PRINT '=== APRES fix ===';
SELECT Code, Libelle, OrdreGroupe, Actif
FROM GroupeImpressionRef
WHERE Libelle LIKE N'Salaire brut%'
   OR Libelle LIKE N'Total Cotisations Sociales%'
   OR Libelle LIKE N'Total Retenues Fiscales%'
   OR Libelle LIKE N'Total Autres Retenues%'
ORDER BY OrdreGroupe;

-- Doublons eventuels (par Libelle)
PRINT '=== Doublons eventuels ===';
SELECT Libelle, COUNT(*) AS NbLignes
FROM GroupeImpressionRef
WHERE Libelle LIKE N'Salaire brut%'
   OR Libelle LIKE N'Total Cotisations Sociales%'
   OR Libelle LIKE N'Total Retenues Fiscales%'
   OR Libelle LIKE N'Total Autres Retenues%'
GROUP BY Libelle
HAVING COUNT(*) > 1;
