-- ============================================================================
-- V1.8.4 - Fix : la Prime de Transport ne doit pas être dans la base IPRES
-- ----------------------------------------------------------------------------
-- Directive RH ELTON : la prime de transport est NON imposable ET HORS IPRES.
--
-- Avant : TRANS héritait BrutSocial=true du TypeRef INDEM_NON_IMPOSA
-- Après : BrutSocial=false sur la rubrique TRANS (override individuel)
--
-- Idempotent : skip si déjà à false.
-- ============================================================================

-- 1. Audit avant
SELECT Code, Libelle, BrutFiscal, BrutSocial,
       (SELECT Code FROM RubriqueTypeRef WHERE Oid = r.TypeRef) AS TypeRef
FROM Rubrique r
WHERE Code = 'TRANS' AND GCRecord IS NULL;

-- 2. Correction
UPDATE Rubrique
SET BrutSocial = 0
WHERE Code = 'TRANS' AND GCRecord IS NULL AND BrutSocial = 1;

PRINT 'Rubrique TRANS : BrutSocial mis à false (hors base IPRES)';

-- 3. Audit après
SELECT Code, Libelle, BrutFiscal, BrutSocial
FROM Rubrique
WHERE Code = 'TRANS' AND GCRecord IS NULL;

-- 4. (Optionnel) Recalculer les bulletins du mois en cours pour purger les
--    IPRES qui incluaient TRANS à tort. À adapter à votre période.
-- Note : XAF recalcul plus pratique via bouton 'Recalculer cotisations' sur
-- chaque bulletin impacté (préserve les rubriques manuelles).
--
-- Pour identifier les bulletins concernés :
SELECT b.Annee, b.Mois, s.Matricule, s.Prenom + ' ' + s.Nom AS Salarie,
       bl.Montant AS PrimeTransport
FROM BulletinLigne bl
INNER JOIN Bulletin b ON b.Oid = bl.Bulletin
INNER JOIN Salarie s ON s.Oid = b.Salarie
INNER JOIN Rubrique r ON r.Oid = bl.Rubrique
WHERE bl.GCRecord IS NULL AND b.GCRecord IS NULL
  AND r.Code = 'TRANS'
  AND b.Annee = YEAR(GETDATE())
ORDER BY b.Mois DESC, s.Matricule;
