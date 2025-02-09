using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rafy;
using Rafy.Domain.ORM;
using Rafy.Domain;
using Rafy.Domain.Validation;
using Rafy.MetaModel;
using Rafy.MetaModel.Attributes;
using Rafy.MetaModel.View;
using Rafy.ManagedProperty;
using System.Security.Permissions;
using System.Runtime.Serialization;

namespace UT
{
    [RootEntity]
    public partial class A : UnitTestEntity
    {
        #region 引用属性

        #endregion

        #region 组合子属性

        public static readonly ListProperty<AChildList> AChildListProperty = P<A>.RegisterList(e => e.AChildList);
        public AChildList AChildList
        {
            get { return this.GetLazyList(AChildListProperty); }
        }

        #endregion

        #region 一般属性

        public static readonly Property<string> NameProperty = P<A>.Register(e => e.Name);
        /// <summary>
        /// 名称
        /// </summary>
        public string Name
        {
            get { return this.GetProperty(NameProperty); }
            set { this.SetProperty(NameProperty, value); }
        }

        public static readonly Property<AType> TypeProperty = P<A>.Register(e => e.Type);
        /// <summary>
        /// AType
        /// </summary>
        public AType Type
        {
            get { return this.GetProperty(TypeProperty); }
            set { this.SetProperty(TypeProperty, value); }
        }

        public static readonly Property<string> Value1Property = P<A>.Register(e => e.Value1);
        /// <summary>
        /// 
        /// </summary>
        public string Value1
        {
            get { return this.GetProperty(Value1Property); }
            set { this.SetProperty(Value1Property, value); }
        }

        public static readonly Property<string> Value2Property = P<A>.Register(e => e.Value2);
        /// <summary>
        /// 
        /// </summary>
        public string Value2
        {
            get { return this.GetProperty(Value2Property); }
            set { this.SetProperty(Value2Property, value); }
        }

        #endregion

        #region 只读属性

        #endregion
    }

    public partial class AList : UnitTestEntityList { }

    public partial class ARepository : UnitTestEntityRepository
    {
        protected ARepository() { }
    }

    internal class AConfig : UnitTestEntityConfig<A>
    {
        protected override void ConfigMeta()
        {
            Meta.MapTable().MapAllProperties();
            //Meta.Property(A.NameProperty).MapColumn().HasLength("200").HasIndex();
        }
    }

    public enum AType
    {
        X, Y
    }
}