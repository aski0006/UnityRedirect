#if UNITY_EDITOR
using UnityEditor;
#endif
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

        [Header("场景引用（TargetType = Scene 时使用）")]
#if UNITY_EDITOR
        [SerializeField] private SceneAsset sceneAsset;
#endif
        [SerializeField] private string scenePath;

        [Header("场景物体引用（GameObjectInScene）")]
        [SerializeField] private GameObject directReference;

        [Header("Prefab 引用（Prefab）")]
        [SerializeField] private GameObject prefabReference;

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

#if UNITY_EDITOR
        /// <summary>场景资产引用（编辑器拖拽用）</summary>
        public SceneAsset SceneAsset => sceneAsset;
#endif

        /// <summary>场景路径（运行时使用，编辑器下由 OnValidate 自动填充）</summary>
        public string ScenePath => scenePath;

        /// <summary>场景物体直接引用</summary>
        public GameObject DirectReference => directReference;

        /// <summary>Prefab 引用</summary>
        public GameObject PrefabReference => prefabReference;

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

#if UNITY_EDITOR
            if (sceneAsset != null)
            {
                scenePath = AssetDatabase.GetAssetPath(sceneAsset);
            }
#endif
        }
    }
}
