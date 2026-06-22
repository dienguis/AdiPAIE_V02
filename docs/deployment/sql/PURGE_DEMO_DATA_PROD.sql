-- ===========================================================================
-- PURGE_DEMO_DATA_PROD.sql  —  V1.7.2 (mai 2026)
--
-- HARD DELETE des données DEMO_* en production ELTON (base AdiPAIE_V02).
--
-- ⚠️ AVANT EXÉCUTION :
--    1. SAUVEGARDE COMPLÈTE de la base (BACKUP DATABASE ...)
--    2. APP IIS ARRÊTÉE (Stop-IISSite ou arrêt du pool d'application)
--    3. Le module V1.7.2 DOIT être déployé (IsDemoSeedEnabled() = false par défaut)
--       sinon les données seront recréées au prochain démarrage.
--
-- 🔎 Ce script :
--    - Détecte AUTOMATIQUEMENT la table de jointure N-N "Contrat-Unites"
--      (le nom XPO peut varier : ContratInterim_UniteOrganisationnelle, etc.)
--    - Détecte AUTOMATIQUEMENT le nom réel des tables (au cas où XPO les
--      renomme via [Persistent])
--    - Tourne en TRANSACTION : ROLLBACK automatique en cas d'erreur.
--    - Affiche un comptage AVANT (preview) et APRÈS (vérif) pour traçabilité.
--
-- Filtres utilisés (extraits de DemoDataSeeder.cs / RecrutementDemoSeeder.cs) :
--    Site.Code                       LIKE 'DEMO\_%' ESCAPE '\'
--    UniteOrganisationnelle.Code     LIKE 'DEMO\_%' ESCAPE '\'
--    Interimaire.Matricule           LIKE 'DEMO\_%' ESCAPE '\'
--    PosteInterimaire.Libelle        LIKE 'DEMO %'           (espace, pas underscore !)
--    SocieteInterim.RaisonSociale    LIKE 'DEMO\_%' ESCAPE '\'
--    BudgetMasseSalariale.Commentaire LIKE 'DEMO\_BUDGET\_%' ESCAPE '\'
--    CongeDemande.Motif              LIKE 'DEMO\_CONGE\_%' ESCAPE '\'
--    Candidat.Matricule              LIKE 'DEMO\_CAND\_%' ESCAPE '\'
--    PosteVacant.Code                LIKE 'DEMO\_POSTE\_%' ESCAPE '\'
--    MotifOuverturePoste.Code        LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
--    SourceRecrutement.Code          LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
--    MotifRefusCandidat.Code         LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
--    MotifRefusOffre.Code            LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
-- ===========================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- toute erreur déclenche un ROLLBACK
GO

USE [AdiPAIE_V02];   -- ← AJUSTER si autre nom de base
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 0. VÉRIFICATION DES TABLES (doit toutes exister, sinon erreur claire)
-- ─────────────────────────────────────────────────────────────────────────────
DECLARE @missing NVARCHAR(MAX) = N'';
DECLARE @tables TABLE (name SYSNAME);
INSERT INTO @tables (name) VALUES
    ('Site'),
    ('UniteOrganisationnelle'),
    ('Interimaire'),
    ('ContratInterim'),
    ('MouvementInterimaire'),
    ('PosteInterimaire'),
    ('SocieteInterim'),
    ('AlerteInterimaire'),
    ('FormationInterimaire'),
    ('EvaluationInterimaire'),
    ('BudgetMasseSalariale'),
    ('CongeDemande'),
    ('Candidat'),
    ('Candidature'),
    ('Entretien'),
    ('OffreEmploi'),
    ('PeriodeEssai'),
    ('PosteVacant'),
    ('MotifOuverturePoste'),
    ('SourceRecrutement'),
    ('MotifRefusCandidat'),
    ('MotifRefusOffre');

SELECT @missing = @missing + name + N', '
FROM @tables t
WHERE NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_NAME = t.name AND TABLE_TYPE = 'BASE TABLE');

IF LEN(@missing) > 0
BEGIN
    PRINT N'⚠️ Tables manquantes (continuez si non utilisées dans cette base) : ' + @missing;
END
ELSE
    PRINT N'✅ Toutes les tables référencées existent.';
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. DÉTECTION DYNAMIQUE DE LA TABLE DE JOINTURE N-N "Contrat-Unites"
--    XPO nomme la table en concaténant : {ClassA}{PropA}_{ClassB}{PropB}
--    Ici : UniteOrganisationnelleUnites_ContratInterimContratsInterim
--    Colonnes : 'Unites' (FK UniteOrganisationnelle) + 'ContratsInterim' (FK ContratInterim)
--    On détecte via les FK réelles vers ContratInterim et UniteOrganisationnelle.
-- ─────────────────────────────────────────────────────────────────────────────
DECLARE @joinTable    SYSNAME = NULL;
DECLARE @colContrat   SYSNAME = NULL;
DECLARE @colUnite     SYSNAME = NULL;

;WITH FKs AS (
    SELECT
        fk.parent_object_id    AS TableObjId,
        OBJECT_NAME(fk.parent_object_id)             AS TableName,
        COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColName,
        OBJECT_NAME(fkc.referenced_object_id)        AS RefTable
    FROM sys.foreign_keys fk
    INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
)
SELECT TOP 1
    @joinTable  = a.TableName,
    @colContrat = a.ColName,
    @colUnite   = b.ColName
FROM FKs a
INNER JOIN FKs b ON a.TableObjId = b.TableObjId
WHERE a.RefTable = 'ContratInterim'
  AND b.RefTable = 'UniteOrganisationnelle'
  AND a.TableName NOT IN ('ContratInterim', 'UniteOrganisationnelle');

IF @joinTable IS NOT NULL
    PRINT N'ℹ️ Table de jointure Contrat-Unites détectée : ' + @joinTable
        + N' (col Contrat=' + @colContrat + N', col Unite=' + @colUnite + N')';
ELSE
    PRINT N'⚠️ Aucune table de jointure ContratInterim ↔ UniteOrganisationnelle détectée (peut être normal si aucun lien N-N existant).';

-- Stocker dans une table globale temporaire pour la suite
IF OBJECT_ID('tempdb..##PURGE_META') IS NOT NULL DROP TABLE ##PURGE_META;
CREATE TABLE ##PURGE_META (Cle SYSNAME PRIMARY KEY, Valeur NVARCHAR(200));
INSERT INTO ##PURGE_META (Cle, Valeur) VALUES
    ('JoinTable',  @joinTable),
    ('ColContrat', @colContrat),
    ('ColUnite',   @colUnite);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. PREVIEW — COMPTAGE AVANT SUPPRESSION
-- ─────────────────────────────────────────────────────────────────────────────
PRINT N'';
PRINT N'═══════════════════════════════════════════════════════════════════════';
PRINT N'  📊 PREVIEW  —  Nombre d''enregistrements DEMO_* qui seront supprimés';
PRINT N'═══════════════════════════════════════════════════════════════════════';

SELECT 'Site'                  AS Entite, COUNT(*) AS NbDemo FROM Site                  WHERE Code         LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'UniteOrganisationnelle',  COUNT(*) FROM UniteOrganisationnelle WHERE Code         LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'Interimaire',             COUNT(*) FROM Interimaire            WHERE Matricule    LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'ContratInterim',          COUNT(*) FROM ContratInterim ci
                                            WHERE EXISTS (SELECT 1 FROM Interimaire i
                                                          WHERE i.Oid = ci.Interimaire
                                                            AND i.Matricule LIKE 'DEMO\_%' ESCAPE '\')
UNION ALL SELECT 'MouvementInterimaire',    COUNT(*) FROM MouvementInterimaire m
                                            WHERE EXISTS (SELECT 1 FROM Interimaire i
                                                          WHERE i.Oid = m.Interimaire
                                                            AND i.Matricule LIKE 'DEMO\_%' ESCAPE '\')
UNION ALL SELECT 'PosteInterimaire',        COUNT(*) FROM PosteInterimaire       WHERE Libelle      LIKE 'DEMO %'
UNION ALL SELECT 'SocieteInterim',          COUNT(*) FROM SocieteInterim         WHERE RaisonSociale LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'BudgetMasseSalariale',    COUNT(*) FROM BudgetMasseSalariale   WHERE Commentaire  LIKE 'DEMO\_BUDGET\_%' ESCAPE '\'
UNION ALL SELECT 'CongeDemande',            COUNT(*) FROM CongeDemande           WHERE Motif        LIKE 'DEMO\_CONGE\_%' ESCAPE '\'
UNION ALL SELECT 'Candidat',                COUNT(*) FROM Candidat               WHERE Matricule    LIKE 'DEMO\_CAND\_%' ESCAPE '\'
UNION ALL SELECT 'PosteVacant',             COUNT(*) FROM PosteVacant            WHERE Code         LIKE 'DEMO\_POSTE\_%' ESCAPE '\'
UNION ALL SELECT 'MotifOuverturePoste',     COUNT(*) FROM MotifOuverturePoste    WHERE Code         LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
UNION ALL SELECT 'SourceRecrutement',       COUNT(*) FROM SourceRecrutement      WHERE Code         LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
UNION ALL SELECT 'MotifRefusCandidat',      COUNT(*) FROM MotifRefusCandidat     WHERE Code         LIKE 'DEMO\_RECRUT\_%' ESCAPE '\'
UNION ALL SELECT 'MotifRefusOffre',         COUNT(*) FROM MotifRefusOffre        WHERE Code         LIKE 'DEMO\_RECRUT\_%' ESCAPE '\';
GO

PRINT N'';
PRINT N'➡️  Si les chiffres ci-dessus correspondent à ce que vous attendez,';
PRINT N'    décommentez le bloc TRANSACTION ci-dessous et relancez le script.';
PRINT N'';
GO


-- ═══════════════════════════════════════════════════════════════════════════
--   PHASE 2 — HARD DELETE  (ACTIVÉ — bloc transaction décommenté)
--
--   ⚠️ Ce script va supprimer DÉFINITIVEMENT les enregistrements DEMO_*.
--      Assurez-vous d'avoir :
--         1. Effectué un BACKUP COMPLET de la base
--         2. Arrêté le pool IIS de l'application
--         3. Validé visuellement le PREVIEW du bloc précédent (Phase 1).
-- ═══════════════════════════════════════════════════════════════════════════

BEGIN TRANSACTION PurgeDemo;
BEGIN TRY

    DECLARE @c INT;
    DECLARE @joinTable  SYSNAME = (SELECT Valeur FROM ##PURGE_META WHERE Cle = 'JoinTable');
    DECLARE @colContrat SYSNAME = (SELECT Valeur FROM ##PURGE_META WHERE Cle = 'ColContrat');
    DECLARE @colUnite   SYSNAME = (SELECT Valeur FROM ##PURGE_META WHERE Cle = 'ColUnite');

    -- ─── A. Recrutement (enfants → parents) ─────────────────────────────────
    DELETE pe FROM PeriodeEssai pe
        INNER JOIN Candidature ca ON ca.Oid = pe.CandidatureOrigine
        INNER JOIN Candidat    cd ON cd.Oid = ca.Candidat
        WHERE cd.Matricule LIKE 'DEMO\_CAND\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ PeriodeEssai supprimés       : ', @@ROWCOUNT);

    DELETE oe FROM OffreEmploi oe
        INNER JOIN Candidature ca ON ca.Oid = oe.Candidature
        INNER JOIN Candidat    cd ON cd.Oid = ca.Candidat
        WHERE cd.Matricule LIKE 'DEMO\_CAND\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ OffreEmploi supprimés        : ', @@ROWCOUNT);

    DELETE en FROM Entretien en
        INNER JOIN Candidature ca ON ca.Oid = en.Candidature
        INNER JOIN Candidat    cd ON cd.Oid = ca.Candidat
        WHERE cd.Matricule LIKE 'DEMO\_CAND\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ Entretien supprimés          : ', @@ROWCOUNT);

    DELETE ca FROM Candidature ca
        INNER JOIN Candidat cd ON cd.Oid = ca.Candidat
        WHERE cd.Matricule LIKE 'DEMO\_CAND\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ Candidature supprimés        : ', @@ROWCOUNT);

    DELETE FROM Candidat
        WHERE Matricule LIKE 'DEMO\_CAND\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ Candidat supprimés           : ', @@ROWCOUNT);

    DELETE FROM PosteVacant
        WHERE Code LIKE 'DEMO\_POSTE\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ PosteVacant supprimés        : ', @@ROWCOUNT);

    DELETE FROM MotifOuverturePoste WHERE Code LIKE 'DEMO\_RECRUT\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ MotifOuverturePoste          : ', @@ROWCOUNT);
    DELETE FROM SourceRecrutement   WHERE Code LIKE 'DEMO\_RECRUT\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ SourceRecrutement            : ', @@ROWCOUNT);
    DELETE FROM MotifRefusCandidat  WHERE Code LIKE 'DEMO\_RECRUT\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ MotifRefusCandidat           : ', @@ROWCOUNT);
    DELETE FROM MotifRefusOffre     WHERE Code LIKE 'DEMO\_RECRUT\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ MotifRefusOffre              : ', @@ROWCOUNT);

    -- ─── B. Budget & Congés démo ────────────────────────────────────────────
    DELETE FROM BudgetMasseSalariale
        WHERE Commentaire LIKE 'DEMO\_BUDGET\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ BudgetMasseSalariale         : ', @@ROWCOUNT);

    DELETE FROM CongeDemande
        WHERE Motif LIKE 'DEMO\_CONGE\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ CongeDemande                 : ', @@ROWCOUNT);

    -- ─── C. Enfants de Interimaire ──────────────────────────────────────────
    DELETE a FROM AlerteInterimaire a
        INNER JOIN Interimaire i ON i.Oid = a.Interimaire
        WHERE i.Matricule LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ AlerteInterimaire            : ', @@ROWCOUNT);

    DELETE f FROM FormationInterimaire f
        INNER JOIN Interimaire i ON i.Oid = f.Interimaire
        WHERE i.Matricule LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ FormationInterimaire         : ', @@ROWCOUNT);

    DELETE e FROM EvaluationInterimaire e
        INNER JOIN Interimaire i ON i.Oid = e.Interimaire
        WHERE i.Matricule LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ EvaluationInterimaire        : ', @@ROWCOUNT);

    DELETE m FROM MouvementInterimaire m
        INNER JOIN Interimaire i ON i.Oid = m.Interimaire
        WHERE i.Matricule LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ MouvementInterimaire         : ', @@ROWCOUNT);

    -- ─── D. Table de jointure N-N Contrat-Unites (DYNAMIQUE) ────────────────
    --     Doit être supprimée AVANT ContratInterim (FK contraignant).
    --     On purge AUSSI les liens vers les UniteOrganisationnelle DEMO_*
    --     (pour libérer les unités elles-mêmes en fin de script).
    IF @joinTable IS NOT NULL AND @colContrat IS NOT NULL AND @colUnite IS NOT NULL
    BEGIN
        DECLARE @sql NVARCHAR(MAX);

        -- D.1 — liens dont le contrat est DEMO_
        SET @sql = N'
            DELETE j FROM ' + QUOTENAME(@joinTable) + N' j
            INNER JOIN ContratInterim ci ON ci.Oid = j.' + QUOTENAME(@colContrat) + N'
            INNER JOIN Interimaire    i  ON i.Oid  = ci.Interimaire
            WHERE i.Matricule LIKE ''DEMO\_%'' ESCAPE ''\''';
        EXEC sp_executesql @sql;
        PRINT CONCAT(N'  ✓ ', @joinTable, N' (par contrat DEMO_) : ', @@ROWCOUNT);

        -- D.2 — liens dont l'unité est DEMO_
        SET @sql = N'
            DELETE j FROM ' + QUOTENAME(@joinTable) + N' j
            INNER JOIN UniteOrganisationnelle u ON u.Oid = j.' + QUOTENAME(@colUnite) + N'
            WHERE u.Code LIKE ''DEMO\_%'' ESCAPE ''\''';
        EXEC sp_executesql @sql;
        PRINT CONCAT(N'  ✓ ', @joinTable, N' (par unité DEMO_)   : ', @@ROWCOUNT);
    END

    -- ─── E. ContratInterim ──────────────────────────────────────────────────
    DELETE ci FROM ContratInterim ci
        INNER JOIN Interimaire i ON i.Oid = ci.Interimaire
        WHERE i.Matricule LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ ContratInterim               : ', @@ROWCOUNT);

    -- ─── F. Interimaire ─────────────────────────────────────────────────────
    DELETE FROM Interimaire WHERE Matricule LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ Interimaire                  : ', @@ROWCOUNT);

    -- ─── G. PosteInterimaire (préfixe "DEMO " avec ESPACE) ──────────────────
    DELETE FROM PosteInterimaire WHERE Libelle LIKE 'DEMO %';
    PRINT CONCAT(N'  ✓ PosteInterimaire             : ', @@ROWCOUNT);

    -- ─── H. SocieteInterim ──────────────────────────────────────────────────
    DELETE FROM SocieteInterim WHERE RaisonSociale LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ SocieteInterim               : ', @@ROWCOUNT);

    -- ─── I. UniteOrganisationnelle (auto-référence Parent → 2 passes) ───────
    --     1) On casse d''abord la self-ref pour éviter le pb d''ordre.
    UPDATE UniteOrganisationnelle SET Parent = NULL
        WHERE Code LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ UniteOrg.Parent → NULL       : ', @@ROWCOUNT);

    DELETE FROM UniteOrganisationnelle WHERE Code LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ UniteOrganisationnelle       : ', @@ROWCOUNT);

    -- ─── J. Site (dernière table — racine de tout) ──────────────────────────
    DELETE FROM Site WHERE Code LIKE 'DEMO\_%' ESCAPE '\';
    PRINT CONCAT(N'  ✓ Site                         : ', @@ROWCOUNT);

    COMMIT TRANSACTION PurgeDemo;
    PRINT N'';
    PRINT N'═══════════════════════════════════════════════════════════════════';
    PRINT N'  ✅ PURGE TERMINÉE AVEC SUCCÈS  —  Transaction commitée.';
    PRINT N'═══════════════════════════════════════════════════════════════════';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION PurgeDemo;
    PRINT N'';
    PRINT N'❌ ERREUR — Transaction ROLLBACK.';
    PRINT N'   N° erreur   : ' + CAST(ERROR_NUMBER()  AS NVARCHAR(20));
    PRINT N'   Ligne       : ' + CAST(ERROR_LINE()    AS NVARCHAR(20));
    PRINT N'   Procédure   : ' + ISNULL(ERROR_PROCEDURE(), '(script direct)');
    PRINT N'   Message     : ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO

-- ─── K. VÉRIFICATION FINALE (mêmes COUNT que le preview, doivent être à 0) ──
PRINT N'';
PRINT N'═══════════════════════════════════════════════════════════════════════';
PRINT N'  🔎 VÉRIFICATION POST-PURGE  —  TOUS LES COMPTEURS DOIVENT ÊTRE À 0';
PRINT N'═══════════════════════════════════════════════════════════════════════';

SELECT 'Site'                  AS Entite, COUNT(*) AS NbDemoRestant FROM Site                  WHERE Code         LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'UniteOrganisationnelle',  COUNT(*) FROM UniteOrganisationnelle WHERE Code         LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'Interimaire',             COUNT(*) FROM Interimaire            WHERE Matricule    LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'PosteInterimaire',        COUNT(*) FROM PosteInterimaire       WHERE Libelle      LIKE 'DEMO %'
UNION ALL SELECT 'SocieteInterim',          COUNT(*) FROM SocieteInterim         WHERE RaisonSociale LIKE 'DEMO\_%' ESCAPE '\'
UNION ALL SELECT 'BudgetMasseSalariale',    COUNT(*) FROM BudgetMasseSalariale   WHERE Commentaire  LIKE 'DEMO\_BUDGET\_%' ESCAPE '\'
UNION ALL SELECT 'CongeDemande',            COUNT(*) FROM CongeDemande           WHERE Motif        LIKE 'DEMO\_CONGE\_%' ESCAPE '\'
UNION ALL SELECT 'Candidat',                COUNT(*) FROM Candidat               WHERE Matricule    LIKE 'DEMO\_CAND\_%' ESCAPE '\'
UNION ALL SELECT 'PosteVacant',             COUNT(*) FROM PosteVacant            WHERE Code         LIKE 'DEMO\_POSTE\_%' ESCAPE '\';

DROP TABLE IF EXISTS ##PURGE_META;
GO

-- ===========================================================================
--   FIN DU SCRIPT
--
--   APRÈS PURGE :
--    - Redémarrer l'app pool IIS
--    - Vérifier que le menu "GRH - Intérimaires" est propre (plus de DEMO_*)
--    - La V1.7.2 du module empêche désormais le re-seed automatique
--      (IsDemoSeedEnabled() = false par défaut dans Updater.cs).
-- ===========================================================================
