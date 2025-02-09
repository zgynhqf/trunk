﻿/*******************************************************
 * 
 * 作者：胡庆访
 * 创建时间：20100330
 * 说明：所有实体集合类的基类
 * 运行环境：.NET 3.5 SP1
 * 版本号：1.0.3
 * 
 * 历史记录：
 * 创建文件 胡庆访 20100330
 * 添加EntityTreeList和EntityList，并实现ReadFromTable方法 胡庆访 20100402
 * 添加两个非泛型的列表基类 胡庆访 20100920
 * 
*******************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Rafy;
using Rafy.Domain.Caching;
using Rafy.ManagedProperty;
using Rafy.MetaModel;

namespace Rafy.Domain
{
    /// <summary>
    /// 所有实体集合类的基类。
    /// <para>使用 <see cref="EntityList{TEntity}"/> 与使用 <see cref="List{T}"/> 的区别在于：</para>
    /// <para>在 <see cref="EntityList{TEntity}"/> 中移除的实体，都会被此列表记住在 <see cref="DeletedList"/> 中，在最终保存列表时，这些被移除的实体会被从持久层删除。</para>
    /// <para>在 <see cref="EntityList{TEntity}"/> 中添加实体时：</para>
    /// <para>* 列表会把该实体的父列表设计为本列表（见：<see cref="Parent"/> 属性）；</para>
    /// <para>* 列表会把该实体的组合父实体设置为本列表的父实体；</para>
    /// <para>* 如果实体是树型实体，那么还会为实体生成相应的 <see cref="Entity.TreeIndex"/>。</para>
    /// <para>
    /// 另外，需要注意的是：仓库的所有数据查询，都是通过 EntityList 来实现数据传输的。包括：FetchCount（查询数据条数，见：<see cref="TotalCount"/>属性）、FetchFirst（查询单条数据）、FetchList（查询数据列表）。
    /// </para>
    /// <para>综上，<see cref="EntityList{TEntity}"/> 主要用于实现领域实体的列表行为、树列表行为以及数据的传输；如果需要对大量数据进行简单的列表操作，请使用更简单的 <see cref="List{T}"/> 泛型即可。把任意列表转换为<see cref="IEntityList"/>，可使用 <see cref="EntityRepository.CreateList(System.Collections.IEnumerable, bool)"/> 方法。</para>
    /// </summary>
    [Serializable]
    public abstract partial class EntityList<TEntity> : ManagedPropertyObjectList<TEntity>,
        IEntityList, IDirtyAware, IDomainComponent, IEntityListInternal
        where TEntity : Entity
    {
        #region FindRepository

        [NonSerialized]
        private bool _repositoryLoaded;

        [NonSerialized]
        private IRepository _repository;

        /// <summary>
        /// 尝试找到这个实体列表对应的仓库类。
        /// 
        /// 没有标记 RootEntity/ChildEntity 的类型是没有仓库类的，例如所有的条件类型。
        /// </summary>
        /// <returns></returns>
        public IRepository FindRepository()
        {
            if (!this._repositoryLoaded)
            {
                var entityType = this.FindEntityType();
                this._repository = RepositoryFactoryHost.Factory.FindByEntity(entityType);

                this._repositoryLoaded = true;
            }

            return this._repository;
        }

        /// <summary>
        /// 获取该实体列表对应的仓库类，如果没有找到，则抛出异常。
        /// </summary>
        /// <returns></returns>
        public IRepository GetRepository()
        {
            var repo = this.FindRepository();
            if (repo == null) throw new InvalidOperationException($"类型 {this.EntityType.FullName} 没有对应的仓库类，无法使用此方法。");

            return repo;
        }

        /// <summary>
        /// 子类可重写此方法，实现列表对应实体类型的查找逻辑。
        /// </summary>
        /// <returns></returns>
        protected virtual Type FindEntityType() { return typeof(TEntity); }

        #endregion

        #region FindListProperty

        [NonSerialized]
        private IListProperty _listProperty;
        [NonSerialized]
        private HasManyType? _listHasManyType;

        internal void InitListProperty(IListProperty value)
        {
            this._listProperty = value;
        }

        /// <summary>
        /// 查找本实体对应的列表属性
        /// 
        /// 以下情况下返回 null：
        /// * 这是一个根对象的集合。
        /// * 这是一个子对象的集合，但是这个集合不在根对象聚合树中。（没有 this.Parent 属性。）
        /// </summary>
        /// <returns></returns>
        private IListProperty FindListProperty()
        {
            if (_listProperty == null)
            {
                var parentEntity = this.Parent;
                if (parentEntity != null)
                {
                    var enumerator = parentEntity.GetLoadedChildren();
                    while (enumerator.MoveNext())
                    {
                        var current = enumerator.Current;
                        if (current.Value == this)
                        {
                            _listProperty = current.Property as IListProperty;
                            break;
                        }
                    }
                }
            }

            return _listProperty;
        }

        /// <summary>
        /// 当前集合的一对多类型
        /// </summary>
        private HasManyType HasManyType
        {
            get
            {
                if (this._listHasManyType == null)
                {
                    if (_parent != null)
                    {
                        var listProperty = this.FindListProperty();
                        if (listProperty != null)
                        {
                            this._listHasManyType = listProperty.HasManyType;
                        }
                    }
                }

                return this._listHasManyType.GetValueOrDefault(HasManyType.Aggregation);
            }
        }

        #endregion

        /// <summary>
        /// 是否：在添加每一项时，
        /// * 设置：实体的 <see cref="IEntity.ParentList"/> 为当前列表；
        /// * 设置它的父对象为本列表对象的父对象。
        /// 
        /// 此属性大部分情况下，应该都是返回 true。
        /// </summary>
        public bool ResetItemParent { get; set; } = true;

        #region Insert, Remove, Clear

        #region public IDisposable MovingItems

        [NonSerialized]
        private bool _movingItems;

        /// <summary>
        /// 设置本列表是否正在调整元素的位置。
        /// 为使得排序性能更好，可使用此方法声明一个只变更元素顺序的代码块，
        /// 此代码块中，列表会认为所有的 Remove、Add、Set 操作，都只是在维护元素顺序，而不会添加新元素、删除旧元素。
        /// </summary>
        /// <returns></returns>
        public IDisposable MovingItems()
        {
            _movingItems = true;
            return new MovingItemsDisposable<TEntity> { Owner = this };
        }

        private class MovingItemsDisposable<T> : IDisposable
            where T : Entity
        {
            internal EntityList<T> Owner;
            public void Dispose()
            {
                Owner._movingItems = false;
                Owner.OnTreeItemsMoved();
            }
        }

        #endregion

        /// <summary>
        /// 清空所有项。
        /// </summary>
        protected override void ClearItems()
        {
            if (!_movingItems)
            {
                for (int i = 0, c = this.Count; i < c; i++)
                {
                    var item = this[i];
                    this.OnItemRemoving(i, item);
                }
            }

            base.ClearItems();
        }

        /// <summary>
        /// Sets the item.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        protected override void SetItem(int index, TEntity item)
        {
            if (!_movingItems)
            {
                var toDelete = this[index];
                if (toDelete != item)
                {
                    this.OnItemRemoving(index, toDelete);
                    this.OnItemAdding(index, item);

                    base.SetItem(index, item);

                    this.OnItemRemoved(index, toDelete);
                    this.OnItemAdded(index, item);
                }
            }
            else
            {
                base.SetItem(index, item);
            }
        }

        /// <summary>
        /// Removes the item.
        /// </summary>
        /// <param name="index">The index.</param>
        protected override void RemoveItem(int index)
        {
            var item = this[index];

            if (!_movingItems)
            {
                this.OnItemRemoving(index, item);
            }

            base.RemoveItem(index);

            if (!_movingItems)
            {
                this.OnItemRemoved(index, item);
            }
        }

        /// <summary>
        /// Inserts the item.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <exception cref="System.InvalidOperationException">当前列表中已经存在这个实体，添加操作不可用。</exception>
        protected override void InsertItem(int index, TEntity item)
        {
            if (item == null) throw new ArgumentNullException("item");

            if (!_movingItems)
            {
                this.OnItemAdding(index, item);
            }

            base.InsertItem(index, item);

            if (!_movingItems)
            {
                this.OnItemAdded(index, item);
            }
        }

        protected virtual void OnItemAdding(int index, Entity item)
        {
            if (this.SupportTree)
            {
                //注释的原因见：TreeComponentHelper.TryAddToList
                //if (this.IsTreeRootList && item.TreePId != null && !item.IsDeleted)
                //{
                //    throw new InvalidOperationException(string.Format(
                //        "树的根节点列表中不能添加非根节点：{0}。", item
                //    ));
                //}

                if (this.Contains(item))
                    throw new InvalidOperationException(string.Format(
"树的列表中已经存在实体 {0}，不能重复添加。", item
));
            }

            //从已删除列表中删除。
            if (item.IsDeleted)
            {
                (item as ITreeComponent).EachNode(i =>
                {
                    (i as IEntityWithStatus).RevertDeletedStatus();
                    return false;
                });

                if (_deletedList != null)
                {
                    _deletedList.Remove(item);
                }
            }

            if (this.ResetItemParent)
            {
                (item as IDomainComponent).SetParent(this);

                if (_parent != null && this.HasManyType == HasManyType.Composition)
                {
                    item.SetParentEntity(_parent as Entity);
                }
            }
        }

        protected virtual void OnItemRemoving(int index, Entity item)
        {
            //如果是新的对象，则不需要加入到 DeletedList 列表中。
            if (!item.IsNew)
            {
                (item as ITreeComponent).EachNode(i =>
                {
                    i.PersistenceStatus = PersistenceStatus.Deleted;
                    return false;
                });

                this.DeletedList.Add(item);
            }

            if (this.ResetItemParent)
            {
                (item as IDomainComponent).SetParent(null);

                //20151024 1551 移除下面的代码
                //在从列表中移除某个实体时，不能把这个实体的父引用实体也改变，
                //否则，这个实体的父引用是空的，这时假删除功能在更新实体时，会因为外键值不正确，而导致无法更新。
                //另外，所有单元测试跑过后，发现下面这段代码没有用到。
                //而且在删除子实体时去清空它的父引用，也没有什么意义。
                //if (this.HasManyType == HasManyType.Composition)
                //{
                //    item.SetParentEntity(null);
                //}
            }
        }

        protected virtual void OnItemAdded(int index, Entity item)
        {
            this.OnTreeItemInserted(index, item);
        }

        protected virtual void OnItemRemoved(int index, Entity item)
        {
            this.OnTreeItemRemoved(index, item);
        }

        #endregion

        /// <summary>
        /// 对应的实体类型。
        /// </summary>
        public Type EntityType
        {
            get
            {
                var repo = this.FindRepository();
                if (repo != null) return repo.EntityType;

                return this.FindEntityType();
            }
        }

        /// <summary>
        /// 设置组合父对象。
        /// </summary>
        /// <param name="parent">由于外部调用时，已经有 parent 的值了，所以直接传进来。</param>
        public virtual void SetParentEntity(Entity parent)
        {
            if (this.Count > 0)
            {
                var refProperty = this.GetRepository().EntityMeta
                    .FindParentReferenceProperty(true).ManagedProperty.CastTo<IRefProperty>();
                this.EachNode(child =>
                {
                    child.SetParentEntity(parent, refProperty);
                    return false;
                });
            }
        }

        #region ReadRowDirectly

        #endregion

        #region 值的复制

        /// <summary>
        /// 复制目标集合中的所有对象。
        /// * 根据列表中的位置来进行拷贝；
        /// * 多余的会移除（不进入 DeletedList）；
        /// * 不够的会生成新的对象；（如果是新构建的实体，持久化状态也完全拷贝。）
        /// </summary>
        /// <param name="sourceList"></param>
        /// <param name="options"></param>
        public void Clone(IEntityList sourceList, CloneOptions options)
        {
            var repo = this.FindRepository();
            var entityType = this.EntityType;

            //逐项拷贝。
            var thisCount = this.Count;
            for (int i = 0, c = sourceList.Count; i < c; i++)
            {
                var source = sourceList[i];

                Entity entity = null;
                if (i < thisCount)
                {
                    entity = this[i];
                }
                else
                {
                    entity = repo?.New() ?? Entity.New(entityType);
                    this.Add(entity);
                }

                entity.Clone(source, options);
            }

            //直接调用基类的方法，直接删除多余的元素。
            while (this.Count > sourceList.Count)
            {
                base.RemoveItem(sourceList.Count);
            }

            var s = sourceList as EntityList<TEntity>;
            if (s._repository != null)
            {
                this.SetRepo(s._repository);
            }
        }

        #endregion

        /// <summary>
        /// 触发某个路由事件
        /// </summary>
        /// <param name="indicator"></param>
        /// <param name="args"></param>
        protected void RaiseRoutedEvent(EntityRoutedEvent indicator, EventArgs args)
        {
            if (indicator.Type == EntityRoutedEventType.BubbleToTreeParent)
            {
                throw new InvalidOperationException("列表类上只支持 BubbleToParent 的实体路由事件。");
            }

            //如果没有父实体，则直接返回
            var parent = this.Parent;
            if (parent == null) return;

            var arg = new EntityRoutedEventArgs
            {
                Source = this,
                Event = indicator,
                Args = args
            };

            parent.RouteByList(this, arg);
        }

        #region IEntityList

        IReadOnlyList<Entity> IEntityList.Linq => this;

        Entity IEntityList.this[int index]
        {
            get { return this[index]; }
            set { this[index] = value as TEntity; }
        }

        IEnumerator<Entity> IEntityList.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        /// <summary>
        /// 把指定的实体集合都回到本集合中来。
        /// </summary>
        /// <param name="entityList">The list.</param>
        public void AddRange(IEnumerable entityList)
        {
            foreach (Entity item in entityList) { this.Add(item); }
        }

        public void Add(Entity entity)
        {
            if (!(entity is TEntity te)) throw new ArgumentException(nameof(entity));
            base.Add(te);
        }

        public int IndexOf(Entity item)
        {
            if (!(item is TEntity te)) throw new ArgumentException(nameof(item));
            return base.IndexOf(te);
        }

        public void Insert(int index, Entity item)
        {
            if (!(item is TEntity te)) throw new ArgumentException(nameof(item));
            base.Insert(index, te);
        }

        public bool Contains(Entity item)
        {
            if (!(item is TEntity te)) throw new ArgumentException(nameof(item));
            return base.Contains(te);
        }

        public void CopyTo(Entity[] array, int arrayIndex)
        {
            for (int i = 0, c = this.Count; i < c; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        public bool Remove(Entity item)
        {
            if (!(item is TEntity te)) throw new ArgumentException(nameof(item));
            return base.Remove(te);
        }

        ManagedPropertyObjectList<Entity> IEntityListInternal.DeletedListField
        {
            get => this.DeletedListField;
            set => this.DeletedListField = value;
        }

        void IEntityListInternal.LoadData(IEnumerable srcList)
        {
            this.LoadData(srcList);
        }

        void IEntityListInternal.SetRepo(IRepository repository)
        {
            this.SetRepo(repository);
        }

        void IEntityListInternal.InitListProperty(IListProperty value)
        {
            this.InitListProperty(value);
        }

        #endregion
    }

    /// <summary>
    /// 在某些列表需要继承的场景下，才需要使用此类。
    /// </summary>
    public abstract class InheritableEntityList : EntityList<Entity>, IEntityList, IList<Entity>
    {
        protected override Type FindEntityType()
        {
            return EntityMatrix.FindByList(this.GetType()).EntityType;
        }
    }
}
