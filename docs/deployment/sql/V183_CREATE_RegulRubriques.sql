-- ============================================================================
-- V1.8.3 - Création manuelle des rubriques de régularisation IR + TRIMF
-- ----------------------------------------------------------------------------
-- À exécuter UNE SEULE FOIS sur la base recette/prod, sans redéploiement.
-- Idempotent : skip silencieux si déjà créé.
--
-- Crée :
--   1 TypeRef  : REGUL_GAIN
--   4 Rubriques: REGUL_IR_RET, REGUL_IR_GAIN, REGUL_TRIMF_RET, REGUL_TRIMF_GAIN
--
-- Valeurs enum :
--   DefaultTypeCalcul / TypeCalcul : 0=Gain, 1=Retenue
--   DefaultSens : 1=Plus, -1=Moins
-- ============================================================================

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ┌--------------------------------------------------------------------------┐
-- │ 1. TypeRef « REGUL_GAIN »                                                │
-- └--------------------------------------------------------------------------┘

DECLARE @oidTypeRegulGain UNIQUEIDENTIFIER;
DECLARE @oidGroupeSalaireBrut UNIQUEIDENTIFIER;

-- Récupère le groupe d'impression "Total Salaire Brut" (créé au seed)
SELECT TOP 1 @oidGroupeSalaireBrut = Oid
FROM GroupeImpressionRef
WHERE Libelle = 'Total Salaire Brut' AND GCRecord IS NULL;

IF @oidGroupeSalaireBrut IS NULL
BEGIN
    RAISERROR(N'GroupeImpressionRef "Total Salaire Brut" introuvable. Vérifiez le seed.', 16, 1);
    ROLLBACK; RETURN;
END

-- Vérifie si REGUL_GAIN existe déjà
SELECT @oidTypeRegulGain = Oid
FROM RubriqueTypeRef
WHERE Code = 'REGUL_GAIN' AND GCRecord IS NULL;

IF @oidTypeRegulGain IS NULL
BEGIN
    SET @oidTypeRegulGain = NEWID();
    INSERT INTO RubriqueTypeRef
        (Oid, Code, Libelle, Groupe,
         BruteFiscal, BruteSocial,
         DefaultTypeCalcul, DefaultSens,
         Actif, EntreColonne13_1024, EntreColonne14_1024,
         OptimisticLockField, GCRecord)
    VALUES
        (@oidTypeRegulGain, 'REGUL_GAIN', 'Régularisations (gain)', @oidGroupeSalaireBrut,
         0, 0,         -- ni brut fiscal ni social
         0, 1,         -- Gain, Plus
         1, 0, 0,
         0, NULL);
    PRINT '  ✓ TypeRef REGUL_GAIN créé';
END
ELSE
    PRINT '  ⏭ TypeRef REGUL_GAIN existait déjà';

-- ┌--------------------------------------------------------------------------┐
-- │ 2. TypeRef « COTFISC » (déjà existant, on le récupère)                   │
-- └--------------------------------------------------------------------------┘

DECLARE @oidTypeCotFis UNIQUEIDENTIFIER;
SELECT @oidTypeCotFis = Oid
FROM RubriqueTypeRef
WHERE Code = 'COTFISC' AND GCRecord IS NULL;

IF @oidTypeCotFis IS NULL
BEGIN
    RAISERROR(N'TypeRef "COTFISC" introuvable. Anormal - devrait être créé au seed.', 16, 1);
    ROLLBACK; RETURN;
END

-- ┌--------------------------------------------------------------------------┐
-- │ 3. Comptes comptables 447100 (IR) et 447200 (TRIMF)                      │
-- └--------------------------------------------------------------------------┘

DECLARE @oidCompte447100 UNIQUEIDENTIFIER;
DECLARE @oidCompte447200 UNIQUEIDENTIFIER;

SELECT TOP 1 @oidCompte447100 = Oid FROM PlanComptable
WHERE Code = '447100' AND GCRecord IS NULL;

SELECT TOP 1 @oidCompte447200 = Oid FROM PlanComptable
WHERE Code = '447200' AND GCRecord IS NULL;

IF @oidCompte447100 IS NULL OR @oidCompte447200 IS NULL
BEGIN
    PRINT '  ⚠ Comptes 447100 ou 447200 introuvables. Rubriques créées sans compte par défaut.';
    PRINT '    Le RH devra les renseigner manuellement après création.';
END

-- ┌--------------------------------------------------------------------------┐
-- │ 4. Rubrique REGUL_IR_RET (Retenue, ordre 810)                            │
-- └--------------------------------------------------------------------------┘

IF NOT EXISTS (SELECT 1 FROM Rubrique WHERE Code = 'REGUL_IR_RET' AND GCRecord IS NULL)
BEGIN
    INSERT INTO Rubrique
        (Oid, Code, Libelle, TypeRef,
         TypeCalcul, BrutFiscal, BrutSocial,
         OrdreAffichage, Actif,
         CompteDebitDefaut, CompteCreditDefaut,
         Canonique,
         OptimisticLockField, GCRecord)
    VALUES
        (NEWID(), 'REGUL_IR_RET', 'Régul. Impôt IR (à prélever)', @oidTypeCotFis,
         1, 0, 0,           -- Retenue, pas brut
         810, 1,
         NULL, @oidCompte447100,
         NULL,
         0, NULL);
    PRINT '  ✓ Rubrique REGUL_IR_RET créée';
END
ELSE
    PRINT '  ⏭ Rubrique REGUL_IR_RET existait déjà';

-- ┌--------------------------------------------------------------------------┐
-- │ 5. Rubrique REGUL_IR_GAIN (Gain, ordre 811)                              │
-- └--------------------------------------------------------------------------┘

IF NOT EXISTS (SELECT 1 FROM Rubrique WHERE Code = 'REGUL_IR_GAIN' AND GCRecord IS NULL)
BEGIN
    INSERT INTO Rubrique
        (Oid, Code, Libelle, TypeRef,
         TypeCalcul, BrutFiscal, BrutSocial,
         OrdreAffichage, Actif,
         CompteDebitDefaut, CompteCreditDefaut,
         Canonique,
         OptimisticLockField, GCRecord)
    VALUES
        (NEWID(), 'REGUL_IR_GAIN', 'Régul. Impôt IR (à rembourser)', @oidTypeRegulGain,
         0, 0, 0,           -- Gain, pas brut
         811, 1,
         @oidCompte447100, NULL,
         NULL,
         0, NULL);
    PRINT '  ✓ Rubrique REGUL_IR_GAIN créée';
END
ELSE
    PRINT '  ⏭ Rubrique REGUL_IR_GAIN existait déjà';

-- ┌--------------------------------------------------------------------------┐
-- │ 6. Rubrique REGUL_TRIMF_RET (Retenue, ordre 812)                         │
-- └--------------------------------------------------------------------------┘

IF NOT EXISTS (SELECT 1 FROM Rubrique WHERE Code = 'REGUL_TRIMF_RET' AND GCRecord IS NULL)
BEGIN
    INSERT INTO Rubrique
        (Oid, Code, Libelle, TypeRef,
         TypeCalcul, BrutFiscal, BrutSocial,
         OrdreAffichage, Actif,
         CompteDebitDefaut, CompteCreditDefaut,
         Canonique,
         OptimisticLockField, GCRecord)
    VALUES
        (NEWID(), 'REGUL_TRIMF_RET', 'Régul. TRIMF (à prélever)', @oidTypeCotFis,
         1, 0, 0,           -- Retenue, pas brut
         812, 1,
         NULL, @oidCompte447200,
         NULL,
         0, NULL);
    PRINT '  ✓ Rubrique REGUL_TRIMF_RET créée';
END
ELSE
    PRINT '  ⏭ Rubrique REGUL_TRIMF_RET existait déjà';

-- ┌--------------------------------------------------------------------------┐
-- │ 7. Rubrique REGUL_TRIMF_GAIN (Gain, ordre 813)                           │
-- └--------------------------------------------------------------------------┘

IF NOT EXISTS (SELECT 1 FROM Rubrique WHERE Code = 'REGUL_TRIMF_GAIN' AND GCRecord IS NULL)
BEGIN
    INSERT INTO Rubrique
        (Oid, Code, Libelle, TypeRef,
         TypeCalcul, BrutFiscal, BrutSocial,
         OrdreAffichage, Actif,
         CompteDebitDefaut, CompteCreditDefaut,
         Canonique,
         OptimisticLockField, GCRecord)
    VALUES
        (NEWID(), 'REGUL_TRIMF_GAIN', 'Régul. TRIMF (à rembourser)', @oidTypeRegulGain,
         0, 0, 0,           -- Gain, pas brut
         813, 1,
         @oidCompte447200, NULL,
         NULL,
         0, NULL);
    PRINT '  ✓ Rubrique REGUL_TRIMF_GAIN créée';
END
ELSE
    PRINT '  ⏭ Rubrique REGUL_TRIMF_GAIN existait déjà';

COMMIT;
PRINT '';
PRINT '═══════════════════════════════════════════════════════════════════';
PRINT '  V1.8.3 - Rubriques de régularisation créées avec succès';
PRINT '═══════════════════════════════════════════════════════════════════';

-- ┌--------------------------------------------------------------------------┐
-- │ Audit final                                                              │
-- └--------------------------------------------------------------------------┘

SELECT
    r.Code,
    r.Libelle,
    r.OrdreAffichage AS Ordre,
    tr.Code AS TypeRefCode,
    CASE r.TypeCalcul WHEN 0 THEN 'Gain' WHEN 1 THEN 'Retenue' END AS TypeCalcul,
    cd.Code AS CompteDebit,
    cc.Code AS CompteCredit,
    CASE WHEN r.Actif = 1 THEN 'Oui' ELSE 'Non' END AS Actif
FROM Rubrique r
INNER JOIN RubriqueTypeRef tr ON tr.Oid = r.TypeRef
LEFT JOIN PlanComptable cd ON cd.Oid = r.CompteDebitDefaut
LEFT JOIN PlanComptable cc ON cc.Oid = r.CompteCreditDefaut
WHERE r.Code LIKE 'REGUL_%' AND r.GCRecord IS NULL
ORDER BY r.OrdreAffichage;
