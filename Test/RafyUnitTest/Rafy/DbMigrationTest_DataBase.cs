﻿/*******************************************************
 * 
 * 作者：胡庆访
 * 创建时间：20110102
 * 说明：此文件只包含一个类，具体内容见类型注释。
 * 运行环境：.NET 4.0
 * 版本号：1.0.0
 * 
 * 历史记录：
 * 创建文件 胡庆访 20110102
 * 
*******************************************************/

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rafy;
using Rafy.Data;
using Rafy.DbMigration;
using Rafy.DbMigration.History;
using Rafy.DbMigration.Model;
using Rafy.DbMigration.Operations;
using Rafy.DbMigration.SqlServer;
using Rafy.Domain;
using Rafy.Domain.ORM.DbMigration;
using UT;

namespace RafyUnitTest
{
    [TestClass]
    public class DbMigrationDataBaseTest
    {
        [ClassInitialize]
        public static void DbMigrationTest_ClassInitialize(TestContext context)
        {
            ServerTestHelper.ClassInitialize(context);
        }

        [TestMethod]
        public void DMDBT_CreateDatabase()
        {
            using (var context = new RafyDbMigrationContext("Test_TestingDataBase"))
            {
                context.RunDataLossOperation = DataLossOperation.All;
                if (context.DbSetting.ProviderName != DbSetting.Provider_SQLite)
                {
                    context.HistoryRepository = new DbHistoryRepository();
                }

                if (!context.DatabaseExists())
                {
                    var destination = new DestinationDatabase("Test_TestingDataBase");
                    var tmpTable = new Table("TestingTable", destination);
                    tmpTable.AddColumn("Id", DbType.Int32, isPrimaryKey: true);
                    tmpTable.AddColumn("Name", DbType.String);
                    destination.Tables.Add(tmpTable);

                    context.MigrateTo(destination);

                    //历史记录
                    if (context.SupportHistory)
                    {
                        var histories = context.GetHistories();
                        Assert.IsTrue(histories.Count == 3);
                        Assert.IsTrue(histories[2] is CreateDatabase);
                    }

                    //数据库结构
                    Assert.IsTrue(context.DatabaseExists());
                }
            }
        }

        [TestMethod]
        public void DMDBT_DropDatabase()
        {
            //以下代码不能运行，会提示数据库正在被使用
            using (var context = new RafyDbMigrationContext("Test_TestingDataBase"))
            {
                context.RunDataLossOperation = DataLossOperation.All;
                if (context.DbSetting.ProviderName != DbSetting.Provider_SQLite)
                {
                    context.HistoryRepository = new DbHistoryRepository();
                }

                if (context.DatabaseExists() && !(context.DbVersionProvider is EmbadedDbVersionProvider))
                {
                    //context.DeleteDatabase();
                    var database = new DestinationDatabase("Test_TestingDataBase") { Removed = true };
                    context.MigrateTo(database);

                    //历史记录
                    if (context.SupportHistory)
                    {
                        var histories = context.GetHistories();
                        Assert.IsTrue(histories[0] is DropDatabase);
                    }

                    //数据库结构
                    Assert.IsTrue(!context.DatabaseExists());
                }

                if (context.SupportHistory)
                {
                    context.ResetHistory();
                }
            }
        }

        [TestMethod]
        public void DMDBT_DeleteAllTables()
        {
            using (var context = new RafyDbMigrationContext("Test_TestingDataBase"))
            {
                context.RunDataLossOperation = DataLossOperation.All;
                if (context.DbSetting.ProviderName != DbSetting.Provider_SQLite)
                {
                    context.HistoryRepository = new DbHistoryRepository();
                }

                if (context.DatabaseExists() && !(context.DbVersionProvider is EmbadedDbVersionProvider))
                {
                    context.DeleteAllTables();

                    //数据库结构
                    var db = context.DatabaseMetaReader.Read();
                    Assert.AreEqual(2, db.Tables.Count);
                }

                if (context.SupportHistory)
                {
                    context.ResetHistory();
                }
                else
                {
                    context.DeleteAllTables();
                    context.AutoMigrate();
                }
            }
        }

        //public class DMDBT_DropDatabase_Migration : ManualDbMigration
        //{
        //    public override string Database
        //    {
        //        get { return "Test_TestingDataBase"; }
        //    }

        //    protected override void Up()
        //    {
        //        this.AddOperation(new DropDatabase
        //        {
        //            Database = this.Database
        //        });
        //    }

        //    protected override void Down() { }

        //    protected override DateTime GetTimeId()
        //    {
        //        return DateTime.Now;
        //    }

        //    protected override string GetDescription()
        //    {
        //        return "单元测试 - 数据库手工升级 - 删除测试数据库";
        //    }

        //    public override ManualMigrationType Type
        //    {
        //        get { return ManualMigrationType.Schema; }
        //    }
        //}

        //[TestMethod]
        //public void DMT_Backup()
        //{
        //    var fileName = @"D:\RafyUnitTest.bak";

        //    var context = new RafyDbMigrationContext(ConnectionStringNames.Rafy);

        //    var res = context.DbBackuper.BackupDatabase(UnitTestEntity.ConnectionString, fileName, true);

        //    Assert.IsTrue(res.Success);
        //}

        //[TestMethod]
        //public void DMT_Restore()
        //{
        //    var fileName = @"D:\RafyUnitTest.bak";

        //    if (File.Exists(fileName))
        //    {
        //        var context = new RafyDbMigrationContext(ConnectionStringNames.Rafy);

        //        var res = context.DbBackuper.RestoreDatabase(UnitTestEntity.ConnectionString, fileName);

        //        Assert.IsTrue(res.Success);
        //    }
        //}
    }
}