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

        private RouteListPanel _routeListPanel;
        private RouteInspectorPanel _inspectorPanel;
        private PipelinePanel _pipelinePanel;
        private RouteGraphView _graphView;

        private VisualElement _pipelineContent;
        private VisualElement _graphContent;
        private ToolbarButton _tabPipeline;
        private ToolbarButton _tabGraph;

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

            // ── Toolbar ──
            var _refreshBtn = root.Q<ToolbarButton>("refresh-button");
            if (_refreshBtn != null) _refreshBtn.clicked += OnRefresh;
            var _createBtn = root.Q<ToolbarButton>("create-route-button");
            if (_createBtn != null) _createBtn.clicked += OnCreateRoute;
            var _initBtn = root.Q<ToolbarButton>("init-assets-button");
            if (_initBtn != null) _initBtn.clicked += OnInitAssets;

            // ── Panels ──
            var listView = root.Q<ListView>("route-list-view");
            var searchField = root.Q<ToolbarSearchField>("route-search-field");
            _routeListPanel = new RouteListPanel(listView, searchField, OnRouteSelected);

            _inspectorPanel = new RouteInspectorPanel(root, OnRouteUpdated);

            _pipelineContent = root.Q<VisualElement>("pipeline-content");
            _graphContent = root.Q<VisualElement>("graph-content");
            _pipelinePanel = new PipelinePanel(_pipelineContent);

            // ── Tabs ──
            _tabPipeline = root.Q<ToolbarButton>("tab-pipeline");
            _tabGraph = root.Q<ToolbarButton>("tab-graph");

            if (_tabPipeline != null)
                _tabPipeline.clicked += () => SwitchTab(true);
            if (_tabGraph != null)
                _tabGraph.clicked += () => SwitchTab(false);

            // ── Refresh UI ──
            _routeListPanel.SetRoutes(_allRoutes);
            _routeListPanel.Deselect();
            UpdateInspector(null);
            UpdatePipeline(null);

            // Register asset change callback
            EditorApplication.projectChanged += OnProjectChanged;
        }

        // ── Tab Switching ──

        private void SwitchTab(bool showPipeline)
        {
            _pipelineContent.style.display = showPipeline ? DisplayStyle.Flex : DisplayStyle.None;
            _graphContent.style.display = showPipeline ? DisplayStyle.None : DisplayStyle.Flex;

            _tabPipeline.RemoveFromClassList("tab-active");
            _tabGraph.RemoveFromClassList("tab-active");
            (showPipeline ? _tabPipeline : _tabGraph).AddToClassList("tab-active");
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
        }

        private void OnCreateRoute()
        {
            var route = CreateInstance<RouteDefinition>();
            route.name = "NewRouteDefinition";
            _allRoutes.Add(route);

            // Save to disk
            var dir = "Assets/RouteNavigator/Resources";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/RouteDefinition.asset");
            AssetDatabase.CreateAsset(route, path);
            AssetDatabase.SaveAssets();

            _routeListPanel.SetRoutes(_allRoutes);
            _routeListPanel.SelectRoute(route);
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

        private void OnProjectChanged()
        {
            // Re-scan when assets change externally
            LoadAllRoutes();
            _routeListPanel.SetRoutes(_allRoutes);
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







