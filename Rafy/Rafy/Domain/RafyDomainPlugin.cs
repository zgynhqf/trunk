﻿/*******************************************************
 * 
 * 作者：胡庆访
 * 创建时间：20110309
 * 说明：此文件只包含一个类，具体内容见类型注释。
 * 运行环境：.NET 4.0
 * 版本号：1.0.0
 * 
 * 历史记录：
 * 创建文件 胡庆访 20100309
 * 
*******************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rafy;
using Rafy.ComponentModel;
using Rafy.Data;
using Rafy.Data.Providers;
using Rafy.DataPortal;
using Rafy.DbMigration;
using Rafy.DbMigration.SqlServer;
using Rafy.Domain.Caching;
using Rafy.Domain.ORM.DbMigration;
using Rafy.ManagedProperty;
using Rafy.MetaModel;
using Rafy.MetaModel.Attributes;

namespace Rafy.Domain
{
    /// <summary>
    /// Rafy Domain 本身也是一个 DomainPlugin
    /// </summary>
    internal class RafyDomainPlugin : DomainPlugin
    {
        public override void Initialize(IApp app)
        {
            if (RepositoryFactoryHost.Factory == null)
            {
                RepositoryFactoryHost.Factory = new DictionaryRepositoryFactory();
            }
            if (!(PropertyDescriptorFactory.Current is RafyPropertyDescriptorFactory))
            {
                PropertyDescriptorFactory.Current = new RafyPropertyDescriptorFactory();
            }
            if (DomainControllerFactory.Default == null)
            {
                DomainControllerFactory.Default = new DomainControllerFactory();
            }

            app.MetaCreating += OnMetaCompiled;
        }

        private void OnMetaCompiled(object sender, EventArgs e)
        {
            RafyEnvironment.HandleAllPlugins(ProcessTypesInPlugin);
        }

        private static void ProcessTypesInPlugin(IPlugin plugin)
        {
            foreach (var type in plugin.Assembly.GetTypes())
            {
                ServiceLocator.TryAddService(type);
                EntityMatrix.TryAddRepository(type);
                DataProviderComposer.TryAddDataProvider(type);
            }
        }
    }
}