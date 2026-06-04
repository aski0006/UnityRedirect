using System.IO;
using UnityEditor;
using UnityEngine;

namespace Kogane.RouteNavigator.Editor
{
    /// <summary>
    /// Route Navigator 根资产自动初始化工具。
    /// 在编辑器启动或窗口打开时自动创建缺失的 ScriptableObject 资产。
    /// </summary>
    public static class RouteNavigatorAssets
    {
        private const string DatabaseFolder = "Assets/Resources/RouteNavigatorDatabase";

        /// <summary>RouteCore 资产路径</summary>
        public const string RouteCorePath = DatabaseFolder + "/RouteCore.asset";

        /// <summary>RouteRegistry 资产路径</summary>
        public const string RouteRegistryPath = DatabaseFolder + "/RouteRegistry.asset";

        /// <summary>
        /// 确保所有根资产存在，缺失时自动创建。
        /// </summary>
        [MenuItem("Window/Route Navigator/Initialize Assets", false, 3001)]
        public static void EnsureAssetsExist()
        {
            var created = false;

            // Create folder
            if (!Directory.Exists(DatabaseFolder))
            {
                Directory.CreateDirectory(DatabaseFolder);
                AssetDatabase.Refresh();
            }

            // Create RouteCore
            if (!AssetDatabase.LoadAssetAtPath<RouteCore>(RouteCorePath))
            {
                var core = ScriptableObject.CreateInstance<RouteCore>();
                core.name = "RouteCore";
                AssetDatabase.CreateAsset(core, RouteCorePath);
                Debug.Log($"[RouteNavigator] Created RouteCore asset at: {RouteCorePath}");
                created = true;
            }

            // Create RouteRegistry
            if (!AssetDatabase.LoadAssetAtPath<RouteRegistry>(RouteRegistryPath))
            {
                var registry = ScriptableObject.CreateInstance<RouteRegistry>();
                registry.name = "RouteRegistry";
                AssetDatabase.CreateAsset(registry, RouteRegistryPath);
                Debug.Log($"[RouteNavigator] Created RouteRegistry asset at: {RouteRegistryPath}");
                created = true;
            }

            if (created)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 在场景加载或域重载时自动初始化。
        /// </summary>
        [InitializeOnLoadMethod]
        private static void AutoInitialize()
        {
            // Delay to ensure project is fully loaded
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EnsureAssetsExist();
                }
            };
        }
    }
}
