-- ============================================================================
-- V1.8.1 — Initialisation de Categories.EstCadre
-- ----------------------------------------------------------------------------
-- À exécuter UNE SEULE FOIS après déploiement de V1.8.1 si l'auto-init du
-- Updater XAF n'a pas tourné (ou n'a pas couvert tous les cas).
--
-- Règle appliquée : EstCadre = 1 ssi
--   - le libellé COMMENCE par "Cadre" (ignore case)
--   - ET le libellé ne contient PAS le mot "non" (évite "Non cadre")
--
-- Idempotent : on ne touche QUE les lignes où EstCadre est encore = 0 et
-- qui matchent la convention. Les catégories déjà cochées par le RH ne
-- sont jamais modifiées.
-- ============================================================================

-- 1. Audit avant
SELECT
    Oid,
    Intitule,
    EstCadre,
    CASE
        WHEN LOWER(LTRIM(Intitule)) LIKE 'cadre%'
         AND LOWER(' ' + Intitule + ' ') NOT LIKE '% non %'
         AND LOWER(' ' + Intitule + ' ') NOT LIKE '%-non %'
         AND LOWER(' ' + Intitule + ' ') NOT LIKE '% non-%'
        THEN 1
        ELSE 0
    END AS EstCadre_Cible
FROM Categories
WHERE GCRecord IS NULL
ORDER BY Intitule;

-- 2. Mise à jour idempotente
UPDATE Categories
SET EstCadre = 1
WHERE GCRecord IS NULL
  AND ISNULL(EstCadre, 0) = 0
  AND LOWER(LTRIM(Intitule)) LIKE 'cadre%'
  AND LOWER(' ' + Intitule + ' ') NOT LIKE '% non %'
  AND LOWER(' ' + Intitule + ' ') NOT LIKE '%-non %'
  AND LOWER(' ' + Intitule + ' ') NOT LIKE '% non-%';

-- 3. Audit après
SELECT Intitule, EstCadre
FROM Categories
WHERE GCRecord IS NULL
ORDER BY EstCadre DESC, Intitule;

-- 4. (Optionnel) Force recalcul des bulletins du mois en cours pour les
--    non-cadres mal classés (à n'exécuter QUE si des bulletins ont été
--    générés avec le bug). À adapter à votre Annee/Mois.
-- DELETE bl
-- FROM BulletinLigne bl
-- INNER JOIN Bulletin b ON b.Oid = bl.Bulletin
-- INNER JOIN Salarie s ON s.Oid = b.Salarie
-- INNER JOIN Categories c ON c.Oid = s.Categories
-- INNER JOIN Rubrique r ON r.Oid = bl.Rubrique
-- INNER JOIN RubriqueCanonique rc ON rc.Oid = r.Canonique
-- WHERE bl.GCRecord IS NULL
--   AND b.GCRecord IS NULL
--   AND b.Annee = 2026 AND b.Mois = 6
--   AND ISNULL(c.EstCadre, 0) = 0
--   AND rc.Code IN ('IPRES_RC');  -- adapter selon votre table RubriqueCanonique
