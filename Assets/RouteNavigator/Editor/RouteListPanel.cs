using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kogane.RouteNavigator.Editor
{
    public class RouteListPanel
    {
        private readonly ListView _listView;
        private readonly ToolbarSearchField _searchField;
        private readonly Action<RouteDefinition> _onSelected;
        private readonly Action<RouteDefinition, string> _onRenamed;

        private List<RouteDefinition> _allRoutes = new();
        private List<RouteDefinition> _filteredRoutes = new();
        private string _searchFilter = string.Empty;

        private int _renamingIndex = -1;
        private string _pendingRenameValue = string.Empty;

        public RouteListPanel(
            ListView listView,
            ToolbarSearchField searchField,
            Action<RouteDefinition> onSelected,
            Action<RouteDefinition, string> onRenamed)
        {
            _listView = listView;
            _searchField = searchField;
            _onSelected = onSelected;
            _onRenamed = onRenamed;
            ConfigureListView();
            ConfigureSearch();
        }

        private void ConfigureListView()
        {
            _listView.makeItem = () =>
            {
                var container = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        paddingLeft = 8, paddingRight = 8,
                        paddingTop = 4, paddingBottom = 4,
                    }
                };
                var label = new Label
                {
                    name = "route-label",
                    style = { fontSize = 11, unityTextAlign = TextAnchor.MiddleLeft, flexGrow = 1 }
                };
                container.Add(label);
                var textField = new TextField
                {
                    name = "route-rename-field",
                    style = { fontSize = 11, flexGrow = 1, display = DisplayStyle.None }
                };
                container.Add(textField);

                container.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.clickCount == 2 && container.userData is int idx)
                    {
                        StartRenaming(idx);
                        evt.StopPropagation();
                    }
                });
                textField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (container.userData is int idx)
                    {
                        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                        { CommitRenaming(idx); evt.StopPropagation(); }
                        else if (evt.keyCode == KeyCode.Escape)
                        { CancelRenaming(idx); evt.StopPropagation(); }
                    }
                });
                textField.RegisterCallback<FocusOutEvent>(evt =>
                {
                    if (container.userData is int idx && _renamingIndex == idx)
                        CommitRenaming(idx);
                });
                return container;
            };

            _listView.bindItem = (element, index) =>
            {
                if (!(element is VisualElement container) || index >= _filteredRoutes.Count) return;
                var route = _filteredRoutes[index];
                container.userData = index;
                var label = container.Q<Label>("route-label");
                var textField = container.Q<TextField>("route-rename-field");
                if (label == null || textField == null) return;

                label.text = !string.IsNullOrEmpty(route.DisplayName)
                    ? string.Format("{0}  ({1})", route.DisplayName, route.name)
                    : route.name;

                switch (route.TargetType)
                {
                    case RouteTargetType.Scene:
                        label.style.color = new StyleColor(new Color(0.6f, 0.8f, 1.0f)); break;
                    case RouteTargetType.GameObjectInScene:
                        label.style.color = new StyleColor(new Color(0.6f, 1.0f, 0.6f)); break;
                    case RouteTargetType.Prefab:
                        label.style.color = new StyleColor(new Color(1.0f, 0.8f, 0.5f)); break;
                }

                var isRenaming = _renamingIndex == index;
                label.style.display = isRenaming ? DisplayStyle.None : DisplayStyle.Flex;
                textField.style.display = isRenaming ? DisplayStyle.Flex : DisplayStyle.None;
                if (isRenaming)
                {
                    textField.value = _pendingRenameValue;
                    textField.schedule.Execute(() => { textField.Focus(); textField.SelectAll(); });
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
            _searchField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Escape) _searchField.value = string.Empty;
            });
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            _onSelected?.Invoke(selection?.OfType<RouteDefinition>().FirstOrDefault());
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrEmpty(_searchFilter))
                _filteredRoutes = new List<RouteDefinition>(_allRoutes);
            else
                _filteredRoutes = _allRoutes.Where(r =>
                    (r.RouteId?.ToLowerInvariant().Contains(_searchFilter) ?? false) ||
                    (r.DisplayName?.ToLowerInvariant().Contains(_searchFilter) ?? false) ||
                    (r.Description?.ToLowerInvariant().Contains(_searchFilter) ?? false) ||
                    r.name.ToLowerInvariant().Contains(_searchFilter)).ToList();

            _listView.itemsSource = _filteredRoutes;
            _listView.Rebuild();
        }

        private void StartRenaming(int index)
        {
            if (index < 0 || index >= _filteredRoutes.Count) return;
            _renamingIndex = -1;
            _renamingIndex = index;
            _pendingRenameValue = _filteredRoutes[index].name;
            _listView.Rebuild();
        }

        private void CommitRenaming(int index)
        {
            if (_renamingIndex != index) return;
            var route = _filteredRoutes[index];
            var newName = _pendingRenameValue?.Trim();
            _renamingIndex = -1;
            if (!string.IsNullOrEmpty(newName) && newName != route.name)
                _onRenamed?.Invoke(route, newName);
            _listView.Rebuild();
        }

        private void CancelRenaming(int index)
        {
            if (_renamingIndex != index) return;
            _renamingIndex = -1;
            _listView.Rebuild();
        }

        public void SetRoutes(List<RouteDefinition> routes)
        {
            _allRoutes = routes ?? new List<RouteDefinition>();
            ApplyFilter();
        }

        public void Refresh() { _listView.Rebuild(); }

        public void SelectRoute(RouteDefinition route)
        {
            if (route == null) return;
            var index = _filteredRoutes.IndexOf(route);
            if (index >= 0) { _listView.SetSelection(index); _listView.ScrollToItem(index); }
        }

        public void Deselect() { _listView.ClearSelection(); }
    }
}
