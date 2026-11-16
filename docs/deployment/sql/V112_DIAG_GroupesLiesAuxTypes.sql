-- ============================================================================
-- V1.12 - DIAG : voir vers quel groupe pointe chaque TypeRef
-- ============================================================================

-- 1. Tous les groupes (y compris orphelins avec ordre NULL)
PRINT '=== Tous les GroupeImpressionRef ===';
SELECT Oid, Code, Libelle, OrdreGroupe, Actif
FROM GroupeImpressionRef
ORDER BY ISNULL(OrdreGroupe, 999), Libelle;

-- 2. Pour chaque TypeRef, voir quel groupe il pointe
PRINT '=== TypeRef et leur Groupe pointe ===';
SELECT t.Code AS TypeCode,
       t.Libelle AS TypeLibelle,
       g.Oid AS GroupeOid,
       g.Code AS GroupeCode,
       g.Libelle AS GroupeLibelle,
       g.OrdreGroupe
FROM RubriqueTypeRef t
LEFT JOIN GroupeImpressionRef g ON t.Groupe = g.Oid
ORDER BY ISNULL(g.OrdreGroupe, 99), t.Code;

-- 3. TypeRef qui pointent vers un groupe avec OrdreGroupe NULL ou 0
PRINT '=== TypeRef avec Groupe NULL / 0 (= le vrai probleme) ===';
SELECT t.Code AS TypeCode, t.Libelle AS TypeLibelle,
       g.Code AS GroupeCode, g.Libelle AS GroupeLibelle, g.OrdreGroupe
FROM RubriqueTypeRef t
LEFT JOIN GroupeImpressionRef g ON t.Groupe = g.Oid
WHERE g.OrdreGroupe IS NULL OR g.OrdreGroupe = 0;
