/*
 * Copyright (c) Contributors, http://whitecore-sim.org/, http://aurora-sim.org
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the WhiteCore-Sim Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using WhiteCore.Framework.ConsoleFramework;
using WhiteCore.Framework.ModuleLoader;
using WhiteCore.Framework.Utilities;

namespace WhiteCore.DataManager.Migration
{
    public class MigrationManager
    {
        readonly IDataConnector genericData;
        readonly string migratorName;
        readonly List<Migrator> migrators = new List<Migrator>();
        readonly bool validateTables;
        bool executed;
        MigrationOperationDescription operationDescription;
        IRestorePoint restorePoint;
        bool rollback;

        public MigrationManager(IDataConnector genericData, string migratorName, bool validateTables)
        {
            this.genericData = genericData;
            this.migratorName = migratorName;
            this.validateTables = validateTables;
            List<IMigrator> allMigrators = WhiteCoreModuleLoader.PickupModules<IMigrator>();

            foreach (
                IMigrator m in
                    allMigrators.Where(m => m.MigrationName != null).Where(m => m.MigrationName == migratorName))
            {
                migrators.Add((Migrator) m);
            }
        }

        public Version LatestVersion
        {
            get { return GetLatestVersionMigrator().Version; }
        }

        public MigrationOperationDescription GetDescriptionOfCurrentOperation()
        {
            return operationDescription;
        }

        public void DetermineOperation()
        {
            if (migratorName == "")
                return;
            executed = false;
            Version currentVersion = genericData.GetWhiteCoreVersion(migratorName);

            //if there is no WhiteCore version, this is likely an entirely new installation
            if (currentVersion == null)
            {
            	MainConsole.Instance.InfoFormat("[Migrator]: Clean installation for {0} found", migratorName);
            	Migrator defaultMigrator = GetHighestVersionMigratorThatCanProvideDefaultSetup();
                currentVersion = defaultMigrator.Version;
                Migrator startMigrator = GetMigratorAfterVersion(defaultMigrator.Version);
                var latestMigrator = GetLatestVersionMigrator();
                Migrator targetMigrator = defaultMigrator == latestMigrator ? null : latestMigrator;
                operationDescription =
                    new MigrationOperationDescription(MigrationOperationTypes.CreateDefaultAndUpgradeToTarget,
                                                      currentVersion,
                                                      startMigrator != null ? startMigrator.Version : null,
                                                      targetMigrator != null ? targetMigrator.Version : null);
            }
            else
            {
                Migrator startMigrator = GetMigratorAfterVersion(currentVersion);
                if (startMigrator != null)
                {
                    Migrator targetMigrator = GetLatestVersionMigrator();
                    MainConsole.Instance.InfoFormat("[Migrator]: New migration script for {0} found", migratorName);
                    MainConsole.Instance.CleanInfoFormat ("            Depending upon the amount of {0} data, this process may take quite a while", migratorName);
                    MainConsole.Instance.CleanInfo ("             You may also see warnings and possible timeout errors until this proccess is completed");
                    MainConsole.Instance.CleanInfo ("");

                    operationDescription = new MigrationOperationDescription(MigrationOperationTypes.UpgradeToTarget,
                                                                             currentVersion, startMigrator.Version,
                                                                             targetMigrator.Version);
                }
                else
                {
                    operationDescription = new MigrationOperationDescription(MigrationOperationTypes.DoNothing,
                                                                             currentVersion);
                }
            }
        }

        Migrator GetMigratorAfterVersion(Version version)
        {
            if (version == null)
            {
                return null;
            }

            return
                (from m in migrators orderby m.Version ascending select m).FirstOrDefault(
                    migrator => migrator.Version > version);
        }

        Migrator GetLatestVersionMigrator()
        {
            return (from m in migrators orderby m.Version descending select m).First();
        }

        Migrator GetHighestVersionMigratorThatCanProvideDefaultSetup()
        {
            return (from m in migrators orderby m.Version descending select m).First();
        }

        public void ExecuteOperation()
        {
            if (migratorName == "")
                return;

            if (operationDescription != null && !executed &&
                operationDescription.OperationType != MigrationOperationTypes.DoNothing)
            {
                Migrator currentMigrator = GetMigratorByVersion(operationDescription.CurrentVersion);

                //if we are creating default, do it now
                if (operationDescription.OperationType == MigrationOperationTypes.CreateDefaultAndUpgradeToTarget)
                {
                    try
                    {
                        currentMigrator.CreateDefaults(genericData);
                    }
                    catch
                    {
                    }
                    executed = true;
                }

                //lets first validate where we think we are
                bool validated = currentMigrator != null && currentMigrator.Validate(genericData);

                if (!validated && validateTables && currentMigrator != null)
                {
                    //Try rerunning the migrator and then the validation
                    //prepare restore point if something goes wrong
                    MainConsole.Instance.Fatal(string.Format("[Migrator]: Failed to validate migration {0}-{1}, retrying...",
                                                             currentMigrator.MigrationName, currentMigrator.Version));

                    currentMigrator.Migrate(genericData);
                    validated = currentMigrator.Validate(genericData);
                    if (!validated)
                    {
                        SchemaDefinition rec;
                        currentMigrator.DebugTestThatAllTablesValidate(genericData, out rec);
                        MainConsole.Instance.Fatal(string.Format(
                            "[Migrator]: FAILED TO REVALIDATE MIGRATION {0}-{1}, FIXING TABLE FORCIBLY... NEW TABLE NAME {2}",
                            currentMigrator.MigrationName,
                            currentMigrator.Version,
                            rec.Name + "_broken"
                                                       ));
                        genericData.RenameTable(rec.Name, rec.Name + "_broken");
                        currentMigrator.Migrate(genericData);
                        validated = currentMigrator.Validate(genericData);
                        if (!validated)
                        {
                            throw new MigrationOperationException(string.Format(
                                "[Migrator]: Current version {0}-{1} did not validate. Stopping here so we don't cause any trouble. No changes were made.",
                                currentMigrator.MigrationName,
                                currentMigrator.Version
                                                                      ));
                        }
                    }
                }
                //else
                //    MainConsole.Instance.Fatal (string.Format ("Failed to validate migration {0}-{1}, continueing...", currentMigrator.MigrationName, currentMigrator.Version));


                bool restoreTaken = false;
                //Loop through versions from start to end, migrating then validating
                Migrator executingMigrator = GetMigratorByVersion(operationDescription.StartVersion);

                //only restore if we are going to do something
                if (executingMigrator != null)
                {
                    if (validateTables && currentMigrator != null)
                    {
                        //prepare restore point if something goes wrong
                        restorePoint = currentMigrator.PrepareRestorePoint(genericData);
                        restoreTaken = true;
                    }
                }


                while (executingMigrator != null)
                {
                    try
                    {
                        executingMigrator.Migrate(genericData);
                    }
                    catch (Exception ex)
                    {
                        if (currentMigrator != null)
                            throw new MigrationOperationException(string.Format("[Migrator]: Migrating to version {0} failed, {1}.",
                                                                                currentMigrator.Version, ex));
                    }
                    executed = true;
                    validated = executingMigrator.Validate(genericData);

                    //if it doesn't validate, rollback
                    if (!validated && validateTables)
                    {
                        RollBackOperation();
                        if (currentMigrator != null)
                            throw new MigrationOperationException(
                                string.Format("[Migrator]: Migrating to version {0} did not validate. Restoring to restore point.",
                                              currentMigrator.Version));
                    }
                    else
                    {
                        executingMigrator.FinishedMigration(genericData);
                    }

                    if (executingMigrator.Version == operationDescription.EndVersion)
                        break;

                    executingMigrator = GetMigratorAfterVersion(executingMigrator.Version);
                }

                if (restoreTaken)
                {
                    currentMigrator.ClearRestorePoint(genericData);
                }
            }
        }

        public void RollBackOperation()
        {
            if (operationDescription != null && executed && !rollback && restorePoint != null)
            {
                restorePoint.DoRestore(genericData);
                rollback = true;
            }
        }

        public bool ValidateVersion(Version version)
        {
            return GetMigratorByVersion(version).Validate(genericData);
        }

        Migrator GetMigratorByVersion(Version version)
        {
            if (version == null)
                return null;
            try
            {
                return (from m in migrators where m.Version == version select m).First();
            }
            catch
            {
                return null;
            }
        }
    }
}