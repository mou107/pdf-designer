-- ─────────────────────────────────────────────────────────────
-- Migration manuelle (MariaDB) : ajoute les colonnes de configuration
-- simple au schema existant `report_templates`.
--
-- Contexte : le microservice utilise EnsureCreated() en dev (pas de
-- migrations EF), qui NE modifie PAS une table deja creee. Sur une base
-- pdf_designer preexistante, appliquer ce script UNE fois — sinon le
-- seeding echoue avec « Unknown column 'ConfigJson' ».
--
-- Alternative sans base : lancer en InMemory
--   Database__UseInMemory=true dotnet run
-- ─────────────────────────────────────────────────────────────

ALTER TABLE `report_templates`
  ADD COLUMN IF NOT EXISTS `Description` VARCHAR(500) NULL,
  ADD COLUMN IF NOT EXISTS `Model` INT NOT NULL DEFAULT 1,
  ADD COLUMN IF NOT EXISTS `TableStyle` INT NOT NULL DEFAULT 2,
  ADD COLUMN IF NOT EXISTS `ConfigJson` LONGTEXT NULL,
  ADD COLUMN IF NOT EXISTS `FileNamePattern` VARCHAR(200) NULL,
  ADD COLUMN IF NOT EXISTS `IsDesignerCustomized` TINYINT(1) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS `MasterUpdateAvailable` TINYINT(1) NOT NULL DEFAULT 0;
