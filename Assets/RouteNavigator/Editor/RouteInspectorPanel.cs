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
        private readonly TextField _tagsField;
        private readonly VisualElement _typeDropdownContainer;
        private readonly IMGUIContainer _unityEventIMGUI;
        private readonly Button _deleteButton;
        private readonly Button _locateButton;

        private RouteDefinition _currentRoute;

        // Serialized object helpers
        private SerializedObject _serializedObject;
        private SerializedObject _unityEventSerializedObject;
        private SerializedProperty _unityEventProp;
        private bool _isUpdating;

        public RouteInspectorPanel(VisualElement root, Action<RouteDefinition> onChanged)
        {
            _root = root;
            _onChanged = onChanged;

            _routeIdField = root.Q<TextField>("route-id-field");
            _displayNameField = root.Q<TextField>("display-name-field");
            _descriptionField = root.Q<TextField>("description-field");
            _targetTypeField = root.Q<EnumField>("target-type-field");
            _targetTypeField.Init(RouteTargetType.None);
            _loadModeField = root.Q<EnumField>("load-mode-field");
            _loadModeField.Init(LoadMode.Activate);
            _sceneAssetField = root.Q<ObjectField>("scene-asset-field");
            _scenePathField = root.Q<TextField>("scene-path-field");
            _directRefField = root.Q<ObjectField>("direct-reference-field");
            _prefabField = root.Q<ObjectField>("prefab-reference-field");
            _unloadPreviousToggle = root.Q<Toggle>("unload-previous-toggle");
            _tagsField = root.Q<TextField>("tags-field");
            _typeDropdownContainer = root.Q<VisualElement>("type-dropdown-container");
            _deleteButton = root.Q<Button>("delete-route-button");
            _locateButton = root.Q<Button>("locate-define-button");

            // UnityEvent rendered via IMGUI container with dedicated SerializedObject
            var unityEventContainer = root.Q<VisualElement>("unity-event-container");
            _unityEventIMGUI = new IMGUIContainer(DrawUnityEvent);
            _unityEventIMGUI.style.minHeight = 60;
            _unityEventIMGUI.style.flexGrow = 1;
            _unityEventIMGUI.style.flexShrink = 0;
            unityEventContainer?.Add(_unityEventIMGUI);

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
                    var newValue = (RouteTargetType)evt.newValue;
                    prop.enumValueIndex = (int)newValue;
                    so.ApplyModifiedProperties();
                    UpdateTargetVisibility(newValue);
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

            // Locate the current RouteDefinition in Project window
            if (_locateButton != null)
            {
                _locateButton.clicked += () =>
                {
                    if (_currentRoute != null)
                    {
                        EditorGUIUtility.PingObject(_currentRoute);
                    }
                };
            }

            // Tags: comma-separated string ↔ string[]
            _tagsField.RegisterValueChangedCallback(evt =>
            {
                if (_isUpdating || _currentRoute == null) return;
                var so = GetOrCreateSerializedObject();
                if (so == null) return;
                var prop = so.FindProperty("tags");
                var raw = evt.newValue ?? string.Empty;
                var parts = raw.Split(',', System.StringSplitOptions.RemoveEmptyEntries);
                var tags = new string[parts.Length];
                for (var i = 0; i < parts.Length; i++)
                    tags[i] = parts[i].Trim();
                prop.arraySize = tags.Length;
                for (var i = 0; i < tags.Length; i++)
                {
                    var elem = prop.GetArrayElementAtIndex(i);
                    elem.stringValue = tags[i];
                }
                so.ApplyModifiedProperties();
                NotifyChanged();
            });
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
            var showScene = targetType == RouteTargetType.Scene;
            var showDirect = targetType == RouteTargetType.GameObjectInScene;
            var showPrefab = targetType == RouteTargetType.Prefab;

            _sceneAssetField.style.display = showScene ? DisplayStyle.Flex : DisplayStyle.None;
            _scenePathField.style.display = showScene ? DisplayStyle.Flex : DisplayStyle.None;
            _directRefField.style.display = showDirect ? DisplayStyle.Flex : DisplayStyle.None;
            _prefabField.style.display = showPrefab ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void DrawUnityEvent()
        {
            if (_unityEventSerializedObject == null || _unityEventProp == null) return;

            _unityEventSerializedObject.Update();
            EditorGUILayout.PropertyField(_unityEventProp, true);
            if (_unityEventSerializedObject.hasModifiedProperties)
            {
                _unityEventSerializedObject.ApplyModifiedProperties();
                if (_currentRoute != null)
                {
                    EditorUtility.SetDirty(_currentRoute);
                    NotifyChanged();
                }
            }
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

            // Reset UnityEvent SerializedObject for the new route
            if (_unityEventSerializedObject != null)
            {
                _unityEventSerializedObject.Dispose();
                _unityEventSerializedObject = null;
            }
            _unityEventProp = null;

            if (route == null)
            {
                ClearFields();
                _unityEventIMGUI?.MarkDirtyLayout();
                _isUpdating = false;
                return;
            }

            _unityEventSerializedObject = new SerializedObject(route);
            _unityEventProp = _unityEventSerializedObject.FindProperty("onNavigationComplete");
            _unityEventIMGUI?.MarkDirtyLayout();

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

            // Tags: display as comma-separated string
            _tagsField.value = route.Tags != null ? string.Join(", ", route.Tags) : string.Empty;

            _isUpdating = false;
        }

        private void ClearFields()
        {
            _routeIdField.value = string.Empty;
            _displayNameField.value = string.Empty;
            _descriptionField.value = string.Empty;
            _targetTypeField.value = RouteTargetType.None;
            _loadModeField.value = LoadMode.Activate;
            _sceneAssetField.value = null;
            _scenePathField.value = string.Empty;
            _directRefField.value = null;
            _prefabField.value = null;
            _unloadPreviousToggle.value = false;
            _tagsField.value = string.Empty;

            UpdateTargetVisibility(RouteTargetType.None);
        }
    }
}

