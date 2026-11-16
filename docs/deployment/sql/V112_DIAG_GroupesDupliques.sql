-- ============================================================================
-- V1.12 - DIAGNOSTIC des GroupeImpressionRef dupliques
-- ============================================================================
-- But : identifier pourquoi les sections s'affichent avec OrdreGroupe NULL
-- alors que les 4 groupes attendus ont ordre 1-4.
-- ============================================================================

-- 1. Lister TOUS les GroupeImpressionRef (actifs ou non)
PRINT '=== 1. Tous les GroupeImpressionRef ===';
SELECT Oid, Code, Libelle, NormalizedKey, OrdreGroupe, Actif
FROM GroupeImpressionRef
ORDER BY Libelle, OrdreGroupe;

-- 2. Compter doublons par NormalizedKey
PRINT '=== 2. Doublons par NormalizedKey ===';
SELECT NormalizedKey, COUNT(*) AS NbLignes,
       STRING_AGG(CAST(Oid AS NVARCHAR(36)), ', ') AS Oids,
       STRING_AGG(ISNULL(Code, '(vide)'), ' | ') AS Codes,
       STRING_AGG(CAST(ISNULL(OrdreGroupe, -1) AS NVARCHAR(10)), ' | ') AS Ordres
FROM GroupeImpressionRef
GROUP BY NormalizedKey
HAVING COUNT(*) > 1;

-- 3. Pour chaque RubriqueTypeRef (= TypeRef), voir quel Groupe il pointe
PRINT '=== 3. RubriqueTypeRef et leur Groupe pointe ===';
SELECT t.Code AS TypeCode,
       t.Libelle AS TypeLibelle,
       g.Oid AS GroupeOid,
       g.Code AS GroupeCode,
       g.Libelle AS GroupeLibelle,
       g.OrdreGroupe AS GroupeOrdre
FROM RubriqueTypeRef t
LEFT JOIN GroupeImpressionRef g ON t.Groupe = g.Oid
ORDER BY ISNULL(g.OrdreGroupe, 99), t.Code;

-- 4. Rubriques dont le groupe (via TypeRef) a OrdreGroupe NULL
PRINT '=== 4. Rubriques pointant vers un Groupe avec OrdreGroupe NULL ===';
SELECT r.Code AS RubriqueCode,
       r.Libelle AS RubriqueLibelle,
       t.Code AS TypeCode,
       g.Code AS GroupeCode,
       g.Libelle AS GroupeLibelle,
       g.OrdreGroupe
FROM Rubrique r
LEFT JOIN RubriqueTypeRef t ON r.TypeRef = t.Oid
LEFT JOIN GroupeImpressionRef g ON t.Groupe = g.Oid
WHERE g.OrdreGroupe IS NULL OR g.OrdreGroupe = 0;
