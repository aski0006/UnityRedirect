using UnityEngine;

namespace Kogane.RouteNavigator
{
    /// <summary>
    /// 路由目标类型
    /// </summary>
    public enum RouteTargetType
    {
        /// <summary>场景切换（SceneManager.LoadSceneAsync）</summary>
        Scene,

        /// <summary>场景内已存在的 GameObject（SetActive）</summary>
        GameObjectInScene,

        /// <summary>Prefab 实例化（Instantiate）</summary>
        Prefab,
    }

    /// <summary>
    /// 目标加载模式
    /// </summary>
    public enum LoadMode
    {
        /// <summary>激活已有物体</summary>
        Activate,

        /// <summary>实例化 Prefab</summary>
        Instantiate,

        /// <summary>异步加载场景</summary>
        LoadScene,
    }

    /// <summary>
    /// 路由定义资产。描述一个导航目标的元数据。
    /// </summary>
    [CreateAssetMenu(
        menuName = "Route Navigator/Route Definition",
        fileName = "NewRouteDefinition"
    )]
    public sealed class RouteDefinition : ScriptableObject
    {
        [Header("基本信息")]
        [SerializeField] private string routeId;
        [SerializeField] private string displayName;

        [Header("目标类型")]
        [SerializeField] private RouteTargetType targetType;

        [Header("目标引用")]
        [SerializeField] private GameObject directReference;
        [SerializeField] private GameObject prefabReference;
        [SerializeField] private string scenePath;

        [Header("加载行为")]
        [SerializeField] private LoadMode loadMode;
        [SerializeField] private bool unloadPrevious;

        [Header("标签与备注")]
        [SerializeField] private string[] tags;
        [SerializeField] private string description;

        /// <summary>路由唯一标识</summary>
        public string RouteId => routeId;

        /// <summary>编辑器中显示的名称</summary>
        public string DisplayName => displayName;

        /// <summary>目标类型</summary>
        public RouteTargetType TargetType => targetType;

        /// <summary>场景物体直接引用（TargetType = GameObjectInScene 时使用）</summary>
        public GameObject DirectReference => directReference;

        /// <summary>Prefab 引用（TargetType = Prefab 时使用）</summary>
        public GameObject PrefabReference => prefabReference;

        /// <summary>场景路径（TargetType = Scene 时使用）</summary>
        public string ScenePath => scenePath;

        /// <summary>加载模式</summary>
        public LoadMode LoadMode => loadMode;

        /// <summary>是否卸载上一个场景</summary>
        public bool UnloadPrevious => unloadPrevious;

        /// <summary>标签数组</summary>
        public string[] Tags => tags;

        /// <summary>描述备注</summary>
        public string Description => description;

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(routeId))
            {
                routeId = name;
            }
        }
    }
}
