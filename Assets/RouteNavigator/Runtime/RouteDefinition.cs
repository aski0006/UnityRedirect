#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;

namespace Kogane.RouteNavigator
{
    public enum RouteTargetType
    {
        None = 0,
        Scene,
        GameObjectInScene,
        Prefab,
    }

    public enum LoadMode
    {
        Activate,
        Instantiate,
        LoadScene,
    }

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
        // TODO: TargetResolverInterceptor 中尚未实现 UnloadPrevious 逻辑。
        // 实现时需在加载新场景前，根据上一个 RouteDefinition 的此字段决定是否卸载旧场景。
        [SerializeField] private bool unloadPrevious;

        [Header("标签与备注")]
        [SerializeField] private string[] tags;
        [SerializeField] private string description;

        [Header("导航事件")]
        [SerializeField] private UnityEvent onNavigationComplete = new();

        /// <summary>导航完成时触发（编辑器可配置回调）</summary>
        public UnityEvent OnNavigationComplete => onNavigationComplete;

        public string RouteId => routeId;
        public string DisplayName => displayName;
        public RouteTargetType TargetType => targetType;

#if UNITY_EDITOR
        public SceneAsset SceneAsset => sceneAsset;
#endif

        public string ScenePath => scenePath;
        public GameObject DirectReference => directReference;
        public GameObject PrefabReference => prefabReference;
        public LoadMode LoadMode => loadMode;
        public bool UnloadPrevious => unloadPrevious;
        public string[] Tags => tags;
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
            else
            {
                // 场景引用被移除时清空路径，防止运行时使用残留的旧路径
                scenePath = string.Empty;
            }
#endif
        }
    }
}
