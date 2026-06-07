using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kogane.RouteNavigator.Editor
{
    /// <summary>
    /// Route Navigator 编辑器主窗口。
    /// 三面板布局：路由列表 | 路由检视 | 管道/图
    /// </summary>
    public class RouteNavigatorWindow : EditorWindow
    {
        private const string UxmlPath = "Assets/RouteNavigator/Editor/RouteNavigatorWindow.uxml";
        private const string UssPath = "Assets/RouteNavigator/Editor/RouteNavigatorWindow.uss";

        [SerializeField] private List<RouteDefinition> _allRoutes = new();

        // TODO: GraphView 可视化路由图 — 当前版本不开发，后续版本计划
        private RouteListPanel _routeListPanel;
        private RouteInspectorPanel _inspectorPanel;
        private PipelinePanel _pipelinePanel;

        private VisualElement _pipelineContent;
        private VisualElement _emptyPlaceholder;
        private ScrollView _inspectorScroll;

        // ── Window Registration ──

        [MenuItem("Window/Route Navigator/Editor", false, 3000)]
        public static void Open()
        {
            var window = GetWindow<RouteNavigatorWindow>();
            window.titleContent = new GUIContent("Route Navigator");
            window.minSize = new Vector2(900, 500);
            window.Show();
        }

        // ── Lifecycle ──

        private void OnEnable()
        {
            RouteNavigatorAssets.EnsureAssetsExist();
            LoadAllRoutes();
            TypeSelector.ClearCache();
            BuildUI();
        }

        private void OnDisable()
        {
            SaveAllRoutes();
        }

        // ── UI Construction ──

        private void BuildUI()
        {
            // Load UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree == null)
            {
                Debug.LogError($"[RouteNavigator] UXML not found at: {UxmlPath}");
                return;
            }

            var root = visualTree.Instantiate();
            rootVisualElement.Add(root);
            // Ensure root fills the editor window
            root.style.flexGrow = 1;
            root.style.flexShrink = 0;
            root.style.height = Length.Percent(100);

            // Load USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
            {
                rootVisualElement.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogWarning($"[RouteNavigator] USS not found at: {UssPath}");
            }

            // ── Three-Column Resizable Layout ──
            // Wrap columns in nested TwoPaneSplitView for drag-resize between all three panes
            var bodyContainer = root.Q<VisualElement>("body-container");
            var colLeft = root.Q<VisualElement>("column-left");
            var colCenter = root.Q<VisualElement>("column-center");
            var colRight = root.Q<VisualElement>("column-right");

            if (bodyContainer != null && colLeft != null && colCenter != null && colRight != null)
            {
                // Remove columns from flat layout
                colLeft.RemoveFromHierarchy();
                colCenter.RemoveFromHierarchy();
                colRight.RemoveFromHierarchy();

                // Inner split: center (flexible) | right (fixed 320px)
                var innerSplit = new TwoPaneSplitView
                {
                    fixedPaneIndex = 1,
                    fixedPaneInitialDimension = 320,
                    orientation = TwoPaneSplitViewOrientation.Horizontal,
                    name = "body-split-inner"
                };
                innerSplit.Add(colCenter);
                innerSplit.Add(colRight);

                // Outer split: left (fixed 260px) | inner split (flexible)
                var outerSplit = new TwoPaneSplitView
                {
                    fixedPaneIndex = 0,
                    fixedPaneInitialDimension = 260,
                    orientation = TwoPaneSplitViewOrientation.Horizontal,
                    name = "body-split-outer"
                };
                outerSplit.Add(colLeft);
                outerSplit.Add(innerSplit);

                bodyContainer.Clear();
                bodyContainer.Add(outerSplit);
            }

            // ── Toolbar ──
            var refreshBtn = root.Q<ToolbarButton>("refresh-button");
            if (refreshBtn != null) refreshBtn.clicked += OnRefresh;
            var createBtn = root.Q<ToolbarButton>("create-route-button");
            if (createBtn != null) createBtn.clicked += OnCreateRoute;
            var initBtn = root.Q<ToolbarButton>("init-assets-button");
            if (initBtn != null) initBtn.clicked += OnInitAssets;
            var locateCoreBtn = root.Q<ToolbarButton>("locate-core-button");
            if (locateCoreBtn != null) locateCoreBtn.clicked += OnLocateCore;
            var locateRegistryBtn = root.Q<ToolbarButton>("locate-registry-button");
            if (locateRegistryBtn != null) locateRegistryBtn.clicked += OnLocateRegistry;

            // ── Panels ──
            var listView = root.Q<ListView>("route-list-view");
            var searchField = root.Q<ToolbarSearchField>("route-search-field");
            _routeListPanel = new RouteListPanel(listView, searchField, OnRouteSelected, OnRouteRenamed);

            _inspectorPanel = new RouteInspectorPanel(root, OnRouteUpdated);

            _pipelineContent = root.Q<VisualElement>("pipeline-content");
            _pipelinePanel = new PipelinePanel(_pipelineContent);

            // ── Empty placeholder ──
            _emptyPlaceholder = root.Q<VisualElement>("empty-placeholder");
            _inspectorScroll = root.Q<ScrollView>("inspector-scroll");
            var emptyCreateBtn = root.Q<Button>("empty-create-route-button");
            if (emptyCreateBtn != null) emptyCreateBtn.clicked += OnCreateRoute;

            // ── Refresh UI ──
            _routeListPanel.SetRoutes(_allRoutes);
            _routeListPanel.Deselect();
            UpdateInspector(null);
            UpdatePipeline(null);
            UpdateEmptyState();

            // Register asset change callback
            EditorApplication.projectChanged += OnProjectChanged;
        }

        // ── Event Handlers ──

        private void OnRefresh()
        {
            TypeSelector.ClearCache();
            LoadAllRoutes();
            _routeListPanel.SetRoutes(_allRoutes);
            _routeListPanel.Deselect();
            UpdateInspector(null);
            UpdatePipeline(null);
            UpdateEmptyState();
        }

        private void OnCreateRoute()
        {
            var route = CreateInstance<RouteDefinition>();
            route.name = "NewRouteDefinition";
            _allRoutes.Add(route);

            // Save to Defines subfolder under RouteNavigatorDatabase
            const string definesDir = "Assets/Resources/RouteNavigatorDatabase/Defines";
            if (!Directory.Exists(definesDir))
            {
                Directory.CreateDirectory(definesDir);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath(definesDir + "/RouteDefinition.asset");
            AssetDatabase.CreateAsset(route, path);
            AssetDatabase.SaveAssets();

            _routeListPanel.SetRoutes(_allRoutes);
            _routeListPanel.SelectRoute(route);
            UpdateEmptyState();

            // 显式刷新 Inspector，确保 UnityEvent 正确渲染
            UpdateInspector(route);
            UpdatePipeline(route);
        }

        private void OnRouteSelected(RouteDefinition route)
        {
            UpdateInspector(route);
            UpdatePipeline(route);
        }

        private void OnRouteUpdated(RouteDefinition route)
        {
            EditorUtility.SetDirty(route);
            _routeListPanel.Refresh();
        }

        private void OnInitAssets()
        {
            RouteNavigatorAssets.EnsureAssetsExist();
            OnRefresh();
        }

        private void OnLocateCore()
        {
            var core = AssetDatabase.LoadAssetAtPath<RouteCore>(
                "Assets/Resources/RouteNavigatorDatabase/RouteCore.asset");
            if (core != null)
            {
                EditorGUIUtility.PingObject(core);
                Selection.activeObject = core;
            }
            else
            {
                Debug.LogWarning("[RouteNavigator] RouteCore.asset not found.");
            }
        }

        private void OnLocateRegistry()
        {
            var registry = AssetDatabase.LoadAssetAtPath<RouteRegistry>(
                "Assets/Resources/RouteNavigatorDatabase/RouteRegistry.asset");
            if (registry != null)
            {
                EditorGUIUtility.PingObject(registry);
                Selection.activeObject = registry;
            }
            else
            {
                Debug.LogWarning("[RouteNavigator] RouteRegistry.asset not found.");
            }
        }

        private void OnProjectChanged()
        {
            // Re-scan when assets change externally
            LoadAllRoutes();
            _routeListPanel.SetRoutes(_allRoutes);
            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            var hasRoutes = _allRoutes.Count > 0;
            if (_emptyPlaceholder != null)
                _emptyPlaceholder.style.display = hasRoutes ? DisplayStyle.None : DisplayStyle.Flex;
            if (_inspectorScroll != null)
                _inspectorScroll.style.display = hasRoutes ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ── Data Management ──

        private void LoadAllRoutes()
        {
            var guids = AssetDatabase.FindAssets("t:RouteDefinition");
            _allRoutes = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<RouteDefinition>(
                    AssetDatabase.GUIDToAssetPath(g)))
                .Where(r => r != null)
                .OrderBy(r => r.name)
                .ToList();
        }


        private void OnRouteRenamed(RouteDefinition route, string newName)
        {
            if (route == null || string.IsNullOrEmpty(newName)) return;

            var assetPath = AssetDatabase.GetAssetPath(route);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"[RouteNavigator] Cannot find asset path for route: {route.name}");
                return;
            }

            AssetDatabase.RenameAsset(assetPath, newName);
            AssetDatabase.SaveAssets();

            // Refresh UI
            _routeListPanel.Refresh();
            _routeListPanel.SetRoutes(_allRoutes);
            _routeListPanel.SelectRoute(route);
        }

        private void SaveAllRoutes()
        {
            AssetDatabase.SaveAssets();
        }

        private void UpdateInspector(RouteDefinition route)
        {
            _inspectorPanel.Display(route);
        }

        private void UpdatePipeline(RouteDefinition route)
        {
            _pipelinePanel.Display(route);
        }
    }
}







