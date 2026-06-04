using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kogane.RouteNavigator.Editor
{
    /// <summary>
    /// 路由列表面板。
    /// 左侧面板：展示所有 RouteDefinition 资产的列表，支持搜索和选择。
    /// </summary>
    public class RouteListPanel
    {
        private readonly ListView _listView;
        private readonly ToolbarSearchField _searchField;
        private readonly Action<RouteDefinition> _onSelected;

        private List<RouteDefinition> _allRoutes = new();
        private List<RouteDefinition> _filteredRoutes = new();
        private string _searchFilter = string.Empty;

        public RouteListPanel(
            ListView listView,
            ToolbarSearchField searchField,
            Action<RouteDefinition> onSelected)
        {
            _listView = listView;
            _searchField = searchField;
            _onSelected = onSelected;

            ConfigureListView();
            ConfigureSearch();
        }

        private void ConfigureListView()
        {
            _listView.makeItem = () =>
            {
                var label = new Label
                {
                    style =
                    {
                        paddingLeft = 8,
                        paddingRight = 8,
                        paddingTop = 4,
                        paddingBottom = 4,
                        fontSize = 11,
                        unityTextAlign = TextAnchor.MiddleLeft,
                    }
                };
                return label;
            };

            _listView.bindItem = (element, index) =>
            {
                if (element is Label label && index < _filteredRoutes.Count)
                {
                    var route = _filteredRoutes[index];
                    label.text = !string.IsNullOrEmpty(route.DisplayName)
                        ? $"{route.DisplayName}  ({route.name})"
                        : route.name;

                    // Color-code by target type
                    switch (route.TargetType)
                    {
                        case RouteTargetType.Scene:
                            label.style.color = new StyleColor(new Color(0.6f, 0.8f, 1.0f));
                            break;
                        case RouteTargetType.GameObjectInScene:
                            label.style.color = new StyleColor(new Color(0.6f, 1.0f, 0.6f));
                            break;
                        case RouteTargetType.Prefab:
                            label.style.color = new StyleColor(new Color(1.0f, 0.8f, 0.5f));
                            break;
                    }
                }
            };

            _listView.itemsSource = _filteredRoutes;

            _listView.onSelectionChange += OnSelectionChanged;
        }

        private void ConfigureSearch()
        {
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue?.ToLowerInvariant() ?? string.Empty;
                ApplyFilter();
            });

            // Clear search on Escape
            _searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape)
                {
                    _searchField.value = string.Empty;
                }
            });
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            var selected = selection?.OfType<RouteDefinition>().FirstOrDefault();
            _onSelected?.Invoke(selected);
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(_searchFilter))
            {
                _filteredRoutes = new List<RouteDefinition>(_allRoutes);
            }
            else
            {
                _filteredRoutes = _allRoutes
                    .Where(r =>
                        (r.RouteId?.ToLowerInvariant().Contains(_searchFilter) ?? false) ||
                        (r.DisplayName?.ToLowerInvariant().Contains(_searchFilter) ?? false) ||
                        (r.Description?.ToLowerInvariant().Contains(_searchFilter) ?? false) ||
                        r.name.ToLowerInvariant().Contains(_searchFilter))
                    .ToList();
            }

            _listView.itemsSource = _filteredRoutes;
            _listView.Rebuild();
        }

        /// <summary>设置路由列表数据源</summary>
        public void SetRoutes(List<RouteDefinition> routes)
        {
            _allRoutes = routes ?? new List<RouteDefinition>();
            ApplyFilter();
        }

        /// <summary>刷新列表显示</summary>
        public void Refresh()
        {
            _listView.Rebuild();
        }

        /// <summary>选中指定路由</summary>
        public void SelectRoute(RouteDefinition route)
        {
            if (route == null) return;

            var index = _filteredRoutes.IndexOf(route);
            if (index >= 0)
            {
                _listView.SetSelection(index);
                _listView.ScrollToItem(index);
            }
        }

        /// <summary>取消选择</summary>
        public void Deselect()
        {
            _listView.ClearSelection();
        }
    }
}
