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
    /// 路由详情检视面板。
    /// 中间面板：展示和编辑选中 RouteDefinition 的所有字段。
    /// </summary>
    public class RouteInspectorPanel
    {
        private readonly VisualElement _root;
        private readonly Action<RouteDefinition> _onChanged;

        // Cache UI references
        private DropdownField _typeDropdown;
        private readonly TextField _routeIdField;
        private readonly TextField _displayNameField;
        private readonly TextField _descriptionField;
        private readonly EnumField _targetTypeField;
        private readonly EnumField _loadModeField;
        private readonly ObjectField _sceneAssetField;
        private readonly TextField _scenePathField;
        private readonly ObjectField _directRefField;
        private readonly ObjectField _prefabField;
        private readonly Toggle _unloadPreviousToggle;
        private readonly VisualElement _tagsContainer;
        private readonly VisualElement _typeDropdownContainer;
        private readonly Button _deleteButton;

        private RouteDefinition _currentRoute;

        // Serialized object helpers
        private SerializedObject _serializedObject;
        private bool _isUpdating;

        public RouteInspectorPanel(VisualElement root, Action<RouteDefinition> onChanged)
        {
            _root = root;
            _onChanged = onChanged;

            _routeIdField = root.Q<TextField>("route-id-field");
            _displayNameField = root.Q<TextField>("display-name-field");
            _descriptionField = root.Q<TextField>("description-field");
            _targetTypeField = root.Q<EnumField>("target-type-field");
            _loadModeField = root.Q<EnumField>("load-mode-field");
            _sceneAssetField = root.Q<ObjectField>("scene-asset-field");
            _scenePathField = root.Q<TextField>("scene-path-field");
            _directRefField = root.Q<ObjectField>("direct-reference-field");
            _prefabField = root.Q<ObjectField>("prefab-reference-field");
            _unloadPreviousToggle = root.Q<Toggle>("unload-previous-toggle");
            _tagsContainer = root.Q<VisualElement>("tags-container");
            _typeDropdownContainer = root.Q<VisualElement>("type-dropdown-container");
            _deleteButton = root.Q<Button>("delete-route-button");

            ConfigureTypeDropdown();
            RegisterFieldCallbacks();
        }

        private void ConfigureTypeDropdown()
        {
            _typeDropdown = TypeSelector.CreateTypeDropdown(
                "Route Data Type",
                null,
                OnTypeSelected);

            _typeDropdownContainer.Clear();
            _typeDropdownContainer.Add(_typeDropdown);
        }

        private void RegisterFieldCallbacks()
        {
            _routeIdField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("routeId");
                if (prop != null)
                {
                    prop.stringValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _displayNameField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("displayName");
                if (prop != null)
                {
                    prop.stringValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _descriptionField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("description");
                if (prop != null)
                {
                    prop.stringValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _targetTypeField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("targetType");
                if (prop != null)
                {
                    prop.enumValueIndex = (int)(RouteTargetType)evt.newValue;
                    so.ApplyModifiedProperties();
                    UpdateTargetVisibility((RouteTargetType)evt.newValue);
                    NotifyChanged();
                }
            });

            _loadModeField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("loadMode");
                if (prop != null)
                {
                    prop.enumValueIndex = (int)(LoadMode)evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _sceneAssetField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var sceneAssetProp = so.FindProperty("sceneAsset");
                var scenePathProp = so.FindProperty("scenePath");
                if (sceneAssetProp != null && scenePathProp != null)
                {
                    sceneAssetProp.objectReferenceValue = evt.newValue;

                    if (evt.newValue is SceneAsset sceneAsset)
                    {
                        scenePathProp.stringValue = AssetDatabase.GetAssetPath(sceneAsset);
                    }
                    else
                    {
                        scenePathProp.stringValue = string.Empty;
                    }

                    so.ApplyModifiedProperties();

                    // Update read-only scene path display
                    _isUpdating = true;
                    _scenePathField.value = scenePathProp.stringValue;
                    _isUpdating = false;

                    NotifyChanged();
                }
            });

            _directRefField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("directReference");
                if (prop != null)
                {
                    prop.objectReferenceValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _prefabField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("prefabReference");
                if (prop != null)
                {
                    prop.objectReferenceValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _unloadPreviousToggle.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("unloadPrevious");
                if (prop != null)
                {
                    prop.boolValue = evt.newValue;
                    so.ApplyModifiedProperties();
                    NotifyChanged();
                }
            });

            _deleteButton.clicked += OnDeleteRoute;
        }

        private void OnTypeSelected(Type type)
        {
            // Type selection changes are informational for now;
            // actual data type binding happens via serialized asset.
        }

        private void OnDeleteRoute()
        {
            if (_currentRoute == null) return;

            if (!EditorUtility.DisplayDialog(
                    "Delete Route",
                    $"Delete route \"{_currentRoute.name}\"?",
                    "Delete", "Cancel"))
                return;

            var path = AssetDatabase.GetAssetPath(_currentRoute);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            _currentRoute = null;
            Display(null);
        }

        private SerializedObject GetOrCreateSerializedObject()
        {
            if (_currentRoute == null) return null;

            if (_serializedObject == null || _serializedObject.targetObject != _currentRoute)
            {
                _serializedObject = new SerializedObject(_currentRoute);
            }

            return _serializedObject;
        }

        private void UpdateTargetVisibility(RouteTargetType targetType)
        {
            _sceneAssetField.style.display = targetType == RouteTargetType.Scene
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _scenePathField.style.display = targetType == RouteTargetType.Scene
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _directRefField.style.display = targetType == RouteTargetType.GameObjectInScene
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            _prefabField.style.display = targetType == RouteTargetType.Prefab
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void NotifyChanged()
        {
            _onChanged?.Invoke(_currentRoute);
        }

        /// <summary>显示指定路由的详情</summary>
        public void Display(RouteDefinition route)
        {
            _currentRoute = route;
            _serializedObject = null;
            _isUpdating = true;

            if (route == null)
            {
                ClearFields();
                _isUpdating = false;
                return;
            }

            _routeIdField.value = route.RouteId ?? string.Empty;
            _displayNameField.value = route.DisplayName ?? string.Empty;
            _descriptionField.value = route.Description ?? string.Empty;
            _targetTypeField.value = route.TargetType;
            _loadModeField.value = route.LoadMode;
            _sceneAssetField.value = route.SceneAsset;
            _scenePathField.value = route.ScenePath ?? string.Empty;
            _directRefField.value = route.DirectReference;
            _prefabField.value = route.PrefabReference;
            _unloadPreviousToggle.value = route.UnloadPrevious;

            UpdateTargetVisibility(route.TargetType);

            // Show scene path display
            _scenePathField.isReadOnly = true;

            // Tags display (read-only summary for now)
            if (route.Tags != null && route.Tags.Length > 0)
            {
                _tagsContainer.Clear();
                foreach (var tag in route.Tags)
                {
                    if (string.IsNullOrEmpty(tag)) continue;

                    var tagLabel = new Label($"#{tag}")
                    {
                        style =
                        {
                            fontSize = 10,
                            color = new StyleColor(new Color(0.5f, 0.7f, 1.0f)),
                            marginRight = 4,
                            marginBottom = 2,
                            paddingLeft = 4,
                            paddingRight = 4,
                            backgroundColor = new StyleColor(new Color(0.2f, 0.3f, 0.4f, 0.5f)),
                            unityTextAlign = TextAnchor.MiddleLeft,
                        }
                    };
                    _tagsContainer.Add(tagLabel);
                }
            }
            else
            {
                _tagsContainer.Clear();
                _tagsContainer.Add(new Label("(none)")
                {
                    style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), fontSize = 10 }
                });
            }

            _isUpdating = false;
        }

        private void ClearFields()
        {
            _routeIdField.value = string.Empty;
            _displayNameField.value = string.Empty;
            _descriptionField.value = string.Empty;
            _targetTypeField.value = RouteTargetType.Scene;
            _loadModeField.value = LoadMode.Activate;
            _sceneAssetField.value = null;
            _scenePathField.value = string.Empty;
            _directRefField.value = null;
            _prefabField.value = null;
            _unloadPreviousToggle.value = false;
            _tagsContainer.Clear();
            _tagsContainer.Add(new Label("(none)")
            {
                style = { color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), fontSize = 10 }
            });

            UpdateTargetVisibility(RouteTargetType.Scene);
        }
    }
}
