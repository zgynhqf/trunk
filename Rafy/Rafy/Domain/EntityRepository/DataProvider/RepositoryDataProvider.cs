﻿/*******************************************************
 * 
 * 作者：胡庆访
 * 创建日期：20150313
 * 运行环境：.NET 4.5
 * 版本号：1.0.0
 * 
 * 历史记录：
 * 创建文件 胡庆访 20150313 23:12
 * 
*******************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Rafy.Data;
using Rafy.Domain.Caching;
using Rafy.Domain.ORM;
using Rafy.Domain.ORM.Query;
using Rafy.ManagedProperty;
using Rafy.MetaModel;

namespace Rafy.Domain
{
    /// <summary>
    /// 通用的仓库数据层实现。
    /// 一般情况下，开发者都可以将查询的实现代码，直接编写在 EntityRepository 中。但是如果开发者想要实现更加通用的 CDUQ 的实现时，可以编写此类的子类来完成。
    /// 
    /// 职责：
    /// * 仓库的数据层实现。
    /// * 整合了 <seealso cref="DataSaver"/> 及 <seealso cref="DataQueryer"/>，及其对应的扩展点。大部分情况下，开发者只需要重写此类的方法即可。
    /// </summary>
    public abstract partial class RepositoryDataProvider : IRepositoryDataProvider,
        ISubmitInterceptor,
        IRepositoryDataProviderInternal
    {
        private EntityRepository _repository;

        private DataQueryer _dataQueryer;

        private DataSaver _dataSaver;

        /// <summary>
        /// 使用的数据库提供程序。
        /// 见：<see cref="DbConnectionSchema"/> 中支持的数据库提供程序类型。
        /// </summary>
        public abstract string DbProviderName { get; }

        /// <summary>
        /// 为此仓库提供数据。
        /// </summary>
        public EntityRepository Repository
        {
            get { return _repository; }
        }

        /// <summary>
        /// 数据的保存器。
        /// </summary>
        public DataSaver DataSaver
        {
            get
            {
                if (_dataSaver == null) throw new InvalidProgramException("请先设置 DataProvider.DataSaver 属性。");
                return _dataSaver;
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                _dataSaver = value;
                value.Init(this);
            }
        }

        /// <summary>
        /// 数据的查询器。
        /// </summary>
        public DataQueryer DataQueryer
        {
            get
            {
                if (_dataQueryer == null) throw new InvalidProgramException("请先设置 DataProvider.DataQueryer 属性。");
                return _dataQueryer;
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                _dataQueryer = value;
                value.Init(this);
            }
        }

        internal void InitRepository(EntityRepository repository)
        {
            _repository = repository;

            if (_dataSaver != null) { _dataSaver.Init(this); }
            if (_dataQueryer != null) { _dataQueryer.Init(this); }
        }

        /// <summary>
        /// 数据门户调用本接口来保存数据。
        /// </summary>
        /// <param name="component"></param>
        internal IDomainComponent SaveComponentByPortal(IDomainComponent component)
        {
            this.DataSaver.SubmitComposition(component);
            return component;
        }

        #region 聚合扩展点

        /*********************** 代码块解释 *********************************
         * 把所有常用的扩展点方法聚合在 DataProvider 中，方便开发者重写。
         * 对于简单的重写逻辑来说，只需要重写此类的方法即可。
         * 对于框架级大型的重写逻辑来说，则需要重写 DataSaver、DataQueryer 中的方法。
        **********************************************************************/

        /// <summary>
        /// 查询实体的事件。
        /// 静态事件，不能经常修改此列表。建议在插件初始化时使用。
        /// </summary>
        public static event EventHandler<QueryingEventArgs> Querying;

        /// <summary>
        /// 子类重写这个方法，用于在从数据库获取出来时，及时地加载一些额外的属性。
        /// 
        /// 注意：这个方法中只应该为一般属性计算值，不能有其它的数据访问。
        /// </summary>
        /// <param name="entity"></param>
        internal protected virtual void OnDbLoaded(Entity entity)
        {
            _dataQueryer.OnDbLoaded(entity);
        }

        /// <summary>
        /// QueryList 方法完成后调用。
        /// 
        /// 子类可重写此方法来实现查询完成后的数据修整工具。
        /// </summary>
        /// <param name="args"></param>
        internal protected virtual void OnEntityQueryed(EntityQueryArgs args)
        {
            _dataQueryer.OnEntityQueryed(args);
        }

        /// <summary>
        /// 所有使用 IQuery 的数据查询，在调用完应 queryBuilder 之后，都会执行此此方法。
        /// 所以子类可以重写此方法实现统一的查询条件逻辑。
        /// （例如，对于映射同一张表的几个子类的查询，可以使用此方法统一对所有方法都过滤）。
        /// 
        /// 默认实现为：
        /// * 如果还没有进行排序，则进行默认的排序。
        /// * 如果单一参数实现了 IPagingCriteria 接口，则使用其中的分页信息进行分页。
        /// </summary>
        /// <param name="args"></param>
        internal protected virtual void OnQuerying(ORMQueryArgs args)
        {
            var h = Querying;
            if (h != null) { h(this, new QueryingEventArgs { Args = args }); }

            _dataQueryer.OnQuerying(args);
        }

        int ISubmitInterceptor.SubmitInterceptorIndex { get; set; }

        /// <summary>
        /// DataProvider 提交拦截器中的最后一个拦截器，直接实现了提交方法。所以不需要调用 Link。
        /// </summary>
        /// <param name="e">The e.</param>
        /// <param name="link">link.</param>
        void ISubmitInterceptor.Submit(SubmitArgs e, ISubmitInterceptorLink link)
        {
            this.Submit(e);
        }

        /// <summary>
        /// 提交聚合对象到数据库中。
        /// 
        /// 子类重写此方法实现整个聚合对象保存到非关系型数据库的逻辑。
        /// 如果只想重写单个对象的 CUD 逻辑，请重写 Insert、Update、Delete 方法。
        /// </summary>
        /// <param name="e"></param>
        internal protected virtual void Submit(SubmitArgs e)
        {
            _dataSaver.Submit(e);
        }

        /// <summary>
        /// 插入这个实体到仓库中。
        /// 
        /// 子类重写此方法来实现非关系型数据库的插入逻辑。
        /// 重写时，注意：
        /// 在插入完成后，把为实体新生成的 Id 赋值到实体中。否则组合子将插入失败。
        /// </summary>
        /// <param name="data"></param>
        internal protected virtual void Insert(Entity data)
        {
            _dataSaver.InsertToPersistence(data);
        }

        /// <summary>
        /// 更新这个实体到仓库中。
        /// 
        /// 子类重写此方法来实现非关系型数据库的更新逻辑。
        /// </summary>
        /// <param name="data"></param>
        internal protected virtual void Update(Entity data)
        {
            _dataSaver.UpdateToPersistence(data);
        }

        /// <summary>
        /// 从仓库中删除这个实体。
        /// 
        /// 子类重写此方法来实现非关系型数据库的删除逻辑。
        /// </summary>
        /// <param name="data"></param>
        internal protected virtual void Delete(Entity data)
        {
            _dataSaver.DeleteFromPersistence(data);
        }

        #endregion
    }

    /// <summary>
    /// 查询实体前的事件的参数。
    /// </summary>
    public class QueryingEventArgs : EventArgs
    {
        /// <summary>
        /// 查询参数
        /// </summary>
        public ORMQueryArgs Args { get; internal set; }
    }
}