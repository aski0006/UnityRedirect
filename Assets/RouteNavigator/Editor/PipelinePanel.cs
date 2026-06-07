using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kogane.RouteNavigator.Editor
{
    /// <summary>
    /// 管道/拦截器链管理面板。
    /// 统一列表：所有拦截器（global + typed）在单一可拖拽列表中管理，
    /// 以 [global] / [typed] 前缀和颜色区分类型。
    /// </summary>
    public class PipelinePanel
    {
        private readonly VisualElement _root;
        private readonly ListView _unifiedList;
        private readonly Button _addButton;

        private RouteDefinition _currentRoute;
        private readonly List<UnifiedEntry> _unifiedConfigs = new();

        /// <summary>
        /// 统一拦截器条目 — 同时承载 global 与 typed 拦截器。
        /// </summary>
        private sealed class UnifiedEntry
        {
            /// <summary>拦截器完整类型名（AssemblyQualifiedName）</summary>
            public string TypeName;
            /// <summary>是否启用</summary>
            public bool Enabled = true;
            /// <summary>Order 覆盖值（0 = 使用代码中的 Order）</summary>
            public int OrderOverride;
            /// <summary>true = global, false = typed</summary>
            public bool IsGlobal;
            /// <summary>TData 类型全名（typed 专属，global 为 null）</summary>
            public string DataTypeFullName;

            /// <summary>显示在列表中的短名称</summary>
            public string ShortTypeName
            {
                get
                {
                    if (string.IsNullOrEmpty(TypeName)) return "(unknown)";
                    // AssemblyQualifiedName: "Namespace.Type, Assembly, ..."
                    var commaIdx = TypeName.IndexOf(',');
                    var fullName = commaIdx >= 0 ? TypeName.Substring(0, commaIdx) : TypeName;
                    var lastDot = fullName.LastIndexOf('.');
                    return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
                }
            }
        }

        // ── Constructor ──

        public PipelinePanel(VisualElement root)
        {
            _root = root;

            _unifiedList = root.Q<ListView>("unified-interceptor-list");
            _addButton = root.Q<Button>("add-interceptor-button");

            ConfigureInterceptorList();
        }

        private void ConfigureInterceptorList()
        {
            _unifiedList.makeItem = () => CreateInterceptorItem();
            _unifiedList.bindItem = (element, index) => BindInterceptorItem(element, index);
            _unifiedList.itemsSource = _unifiedConfigs;
            _unifiedList.reorderable = true;
            _unifiedList.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            _addButton.clicked += () => ShowAddInterceptorDialog();
        }

        // ── Item Creation ──

        private static VisualElement CreateInterceptorItem()
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 8,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    alignItems = Align.FlexStart,
                }
            };

            var dragHandle = new Label
            {
                name = "interceptor-drag-handle",
                text = "≡",
                style =
                {
                    fontSize = 14,
                    marginRight = 4,
                    marginTop = 1,
                    flexGrow = 0,
                    flexShrink = 0,
                    width = 14,
                    color = new StyleColor(new Color(0.35f, 0.35f, 0.35f)),
                    unityTextAlign = TextAnchor.MiddleCenter,
                }
            };

            var kindLabel = new Label
            {
                name = "interceptor-kind",
                style =
                {
                    fontSize = 10,
                    marginRight = 4,
                    marginTop = 2,
                    flexGrow = 0,
                    flexShrink = 0,
                    unityTextAlign = TextAnchor.MiddleLeft,
                }
            };

            var toggle = new Toggle
            {
                name = "interceptor-toggle",
                style =
                {
                    marginRight = 4,
                    marginTop = 1,
                    flexGrow = 0,
                    flexShrink = 0,
                }
            };

            var label = new Label
            {
                name = "interceptor-label",
                style =
                {
                    fontSize = 11,
                    flexGrow = 1,
                    flexShrink = 1,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    whiteSpace = WhiteSpace.Normal,
                    overflow = Overflow.Hidden,
                }
            };

            var deleteButton = new Button
            {
                name = "interceptor-delete",
                text = "×",
                style =
                {
                    fontSize = 12,
                    width = 20,
                    height = 20,
                    minWidth = 20,
                    minHeight = 20,
                    paddingLeft = 0,
                    paddingRight = 0,
                    paddingTop = 0,
                    paddingBottom = 0,
                    backgroundColor = new StyleColor(new Color(0.5f, 0.2f, 0.2f)),
                    color = Color.white,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2,
                    flexGrow = 0,
                    flexShrink = 0,
                }
            };

            container.Add(dragHandle);
            container.Add(kindLabel);
            container.Add(toggle);
            container.Add(label);
            container.Add(deleteButton);

            return container;
        }

        // ── Item Binding ──

        private void BindInterceptorItem(VisualElement element, int index)
        {
            if (index < 0 || index >= _unifiedConfigs.Count) return;

            var entry = _unifiedConfigs[index];

            var kindLabel = element.Q<Label>("interceptor-kind");
            var toggle = element.Q<Toggle>("interceptor-toggle");
            var label = element.Q<Label>("interceptor-label");
            var deleteButton = element.Q<Button>("interceptor-delete");

            // Kind badge
            if (kindLabel != null)
            {
                if (entry.IsGlobal)
                {
                    kindLabel.text = "[global]";
                    kindLabel.style.color = new StyleColor(new Color(0.35f, 0.55f, 0.85f));
                }
                else
                {
                    var typeName = !string.IsNullOrEmpty(entry.DataTypeFullName)
                        ? entry.DataTypeFullName.Substring(entry.DataTypeFullName.LastIndexOf('.') + 1)
                        : "?";
                    kindLabel.text = $"[typed: {typeName}]";
                    kindLabel.style.color = new StyleColor(new Color(0.35f, 0.75f, 0.45f));
                }
            }

            // Toggle
            if (toggle != null)
            {
                toggle.SetValueWithoutNotify(entry.Enabled);
                // Store index for the callback
                toggle.userData = index;
                toggle.UnregisterValueChangedCallback(OnToggleChanged);
                toggle.RegisterValueChangedCallback(OnToggleChanged);
            }

            // Name label
            if (label != null)
            {
                label.text = entry.ShortTypeName;
                label.tooltip = entry.TypeName;
            }

            // Delete button
            if (deleteButton != null)
            {
                deleteButton.clickable = null;
                var capturedIndex = index;
                deleteButton.clicked += () =>
                {
                    DeleteInterceptor(capturedIndex);
                };
            }
        }

        private void OnToggleChanged(ChangeEvent<bool> evt)
        {
            var toggle = evt.target as Toggle;
            if (toggle?.userData is int index && index >= 0 && index < _unifiedConfigs.Count)
            {
                _unifiedConfigs[index].Enabled = evt.newValue;
                SaveUnifiedConfigs();
            }
        }

        // ── Add Interceptor ──

        private void ShowAddInterceptorDialog()
        {
            var menu = new GenericMenu();

            // Scan for available interceptor types
            var globalTypes = new List<Type>();
            var typedTypes = new List<(Type interceptorType, Type dataType)>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic) continue;

                try
                {
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        // 跳过抽象类、接口、值类型、以及开放泛型定义（如 ConditionInterceptor<TData>）
                        if (type.IsAbstract || type.IsInterface || !type.IsClass || type.IsGenericTypeDefinition) continue;

                        bool isGlobal = typeof(INavigationInterceptorBase).IsAssignableFrom(type);

                        // Check if implements INavigationInterceptor<TData>
                        Type foundDataType = null;
                        foreach (var iface in type.GetInterfaces())
                        {
                            if (iface.IsGenericType &&
                                iface.GetGenericTypeDefinition() == typeof(INavigationInterceptor<>))
                            {
                                foundDataType = iface.GetGenericArguments()[0];
                                break;
                            }
                        }

                        if (isGlobal)
                        {
                            globalTypes.Add(type);
                        }
                        if (foundDataType != null)
                        {
                            typedTypes.Add((type, foundDataType));
                        }
                    }
                }
                catch (ReflectionTypeLoadException) { }
            }

            var totalCount = globalTypes.Count + typedTypes.Count;
            if (totalCount == 0)
            {
                menu.AddDisabledItem(new GUIContent("No interceptors found"));
            }
            else
            {
                // ── Global section ──
                if (globalTypes.Count > 0)
                {
                    menu.AddDisabledItem(new GUIContent("── Global ──"));
                    foreach (var type in globalTypes.OrderBy(t => t.Name))
                    {
                        var typeName = type.FullName;
                        var label = $"[global]  {type.Name}";
                        var capturedTypeName = typeName;
                        menu.AddItem(new GUIContent(label), false, () =>
                        {
                            AddInterceptor(capturedTypeName, isGlobal: true, dataTypeFullName: null);
                        });
                    }
                }

                // ── Typed section ──
                if (typedTypes.Count > 0)
                {
                    if (globalTypes.Count > 0)
                        menu.AddSeparator("");
                    menu.AddDisabledItem(new GUIContent("── Typed ──"));
                    foreach (var (interceptorType, dataType) in typedTypes
                        .OrderBy(t => t.interceptorType.Name)
                        .ThenBy(t => t.dataType.Name))
                    {
                        var typeName = interceptorType.FullName;
                        var label = $"[typed: {dataType.Name}]  {interceptorType.Name}";
                        var capturedTypeName = typeName;
                        var capturedDataTypeFullName = dataType.FullName;
                        menu.AddItem(new GUIContent(label), false, () =>
                        {
                            AddInterceptor(capturedTypeName, isGlobal: false,
                                dataTypeFullName: capturedDataTypeFullName);
                        });
                    }
                }
            }

            menu.ShowAsContext();
        }

        private void AddInterceptor(string typeFullName, bool isGlobal, string dataTypeFullName)
        {
            // Resolve the type to get AssemblyQualifiedName for serialization
            Type resolvedType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                resolvedType = asm.GetType(typeFullName);
                if (resolvedType != null) break;
            }
            if (resolvedType == null)
            {
                Debug.LogWarning($"[RouteNavigator] Cannot resolve interceptor type: {typeFullName}");
                return;
            }

            var qualifiedName = resolvedType.AssemblyQualifiedName;

            // ── Duplicate check ──
            // Same type can only exist once in the same context:
            // - Global: check among all global entries
            // - Typed: check among typed entries for the same TData type
            if (isGlobal)
            {
                var exists = _unifiedConfigs.Any(e =>
                    e.IsGlobal &&
                    ExtractComparableTypeName(e.TypeName) == ExtractComparableTypeName(qualifiedName));
                if (exists)
                {
                    Debug.LogWarning($"[RouteNavigator] Global interceptor '{resolvedType.Name}' already exists.");
                    return;
                }
            }
            else
            {
                var exists = _unifiedConfigs.Any(e =>
                    !e.IsGlobal &&
                    e.DataTypeFullName == dataTypeFullName &&
                    ExtractComparableTypeName(e.TypeName) == ExtractComparableTypeName(qualifiedName));
                if (exists)
                {
                    Debug.LogWarning($"[RouteNavigator] Typed interceptor '{resolvedType.Name}' for '{dataTypeFullName}' already exists.");
                    return;
                }
            }

            // Add entry
            var entry = new UnifiedEntry
            {
                TypeName = qualifiedName,
                Enabled = true,
                OrderOverride = 0,
                IsGlobal = isGlobal,
                DataTypeFullName = dataTypeFullName,
            };
            _unifiedConfigs.Add(entry);

            SaveUnifiedConfigs();
            Refresh();
        }

        /// <summary>
        /// Extract the full type name (without assembly suffix) for comparison.
        /// "Namespace.Type, Assembly, ..." → "Namespace.Type"
        /// </summary>
        private static string ExtractComparableTypeName(string assemblyQualifiedName)
        {
            if (string.IsNullOrEmpty(assemblyQualifiedName)) return string.Empty;
            var commaIdx = assemblyQualifiedName.IndexOf(',');
            return commaIdx >= 0 ? assemblyQualifiedName.Substring(0, commaIdx) : assemblyQualifiedName;
        }

        // ── Delete Interceptor ──

        private void DeleteInterceptor(int index)
        {
            if (index < 0 || index >= _unifiedConfigs.Count) return;

            _unifiedConfigs.RemoveAt(index);
            SaveUnifiedConfigs();
            Refresh();
        }

        // ── Persistence ──

        /// <summary>刷新显示指定路由的拦截器配置</summary>
        public void Display(RouteDefinition route)
        {
            _currentRoute = route;
            ReloadUnifiedConfigs();
            Refresh();
        }

        private void ReloadUnifiedConfigs()
        {
            _unifiedConfigs.Clear();

            // ── Load global interceptors from RouteCore ──
            var core = RouteCore.Instance;
            if (core != null)
            {
                var so = new SerializedObject(core);
                var configsProp = so.FindProperty("globalInterceptorConfigs");
                if (configsProp != null)
                {
                    for (var i = 0; i < configsProp.arraySize; i++)
                    {
                        var elem = configsProp.GetArrayElementAtIndex(i);
                        _unifiedConfigs.Add(new UnifiedEntry
                        {
                            TypeName = elem.FindPropertyRelative("typeName")?.stringValue ?? string.Empty,
                            Enabled = elem.FindPropertyRelative("enabled")?.boolValue ?? true,
                            OrderOverride = elem.FindPropertyRelative("orderOverride")?.intValue ?? 0,
                            IsGlobal = true,
                            DataTypeFullName = null,
                        });
                    }
                }
            }

            // ── Load typed interceptors from RouteRegistry ──
            var registry = Resources.Load<RouteRegistry>("RouteNavigatorDatabase/RouteRegistry");
            if (registry != null)
            {
                var so = new SerializedObject(registry);
                var groupsProp = so.FindProperty("typedGroups");
                if (groupsProp != null)
                {
                    for (var g = 0; g < groupsProp.arraySize; g++)
                    {
                        var group = groupsProp.GetArrayElementAtIndex(g);
                        var dataTypeFullName = group.FindPropertyRelative("dataTypeFullName")?.stringValue;
                        var interceptorListProp = group.FindPropertyRelative("interceptors");
                        if (interceptorListProp == null) continue;

                        for (var i = 0; i < interceptorListProp.arraySize; i++)
                        {
                            var elem = interceptorListProp.GetArrayElementAtIndex(i);
                            _unifiedConfigs.Add(new UnifiedEntry
                            {
                                TypeName = elem.FindPropertyRelative("typeName")?.stringValue ?? string.Empty,
                                Enabled = elem.FindPropertyRelative("enabled")?.boolValue ?? true,
                                OrderOverride = elem.FindPropertyRelative("orderOverride")?.intValue ?? 0,
                                IsGlobal = false,
                                DataTypeFullName = dataTypeFullName,
                            });
                        }
                    }
                }
            }
        }

        private void SaveUnifiedConfigs()
        {
            // ── Save global entries back to RouteCore ──
            var core = RouteCore.Instance;
            if (core != null)
            {
                var so = new SerializedObject(core);
                var configsProp = so.FindProperty("globalInterceptorConfigs");
                if (configsProp != null)
                {
                    var globalEntries = _unifiedConfigs.Where(e => e.IsGlobal).ToList();
                    configsProp.arraySize = globalEntries.Count;
                    for (var i = 0; i < globalEntries.Count; i++)
                    {
                        var elem = configsProp.GetArrayElementAtIndex(i);
                        WriteConfigToProperty(elem, globalEntries[i]);
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(core);
            }

            // ── Save typed entries back to RouteRegistry ──
            var registry = Resources.Load<RouteRegistry>("RouteNavigatorDatabase/RouteRegistry");
            if (registry != null)
            {
                var so = new SerializedObject(registry);
                var groupsProp = so.FindProperty("typedGroups");
                if (groupsProp != null)
                {
                    // Group typed entries by DataTypeFullName, preserving order
                    var groupOrder = new List<string>();
                    var groupEntries = new Dictionary<string, List<UnifiedEntry>>();
                    foreach (var entry in _unifiedConfigs.Where(e => !e.IsGlobal))
                    {
                        var key = entry.DataTypeFullName ?? string.Empty;
                        if (!groupEntries.ContainsKey(key))
                        {
                            groupOrder.Add(key);
                            groupEntries[key] = new List<UnifiedEntry>();
                        }
                        groupEntries[key].Add(entry);
                    }

                    groupsProp.arraySize = groupOrder.Count;
                    for (var g = 0; g < groupOrder.Count; g++)
                    {
                        var key = groupOrder[g];
                        var group = groupsProp.GetArrayElementAtIndex(g);
                        var nameProp = group.FindPropertyRelative("dataTypeFullName");
                        if (nameProp != null) nameProp.stringValue = key;

                        var interceptorListProp = group.FindPropertyRelative("interceptors");
                        var entries = groupEntries[key];
                        interceptorListProp.arraySize = entries.Count;
                        for (var i = 0; i < entries.Count; i++)
                        {
                            var elem = interceptorListProp.GetArrayElementAtIndex(i);
                            WriteConfigToProperty(elem, entries[i]);
                        }
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(registry);
            }

            AssetDatabase.SaveAssets();
        }

        private static void WriteConfigToProperty(SerializedProperty prop, UnifiedEntry entry)
        {
            var typeNameProp = prop.FindPropertyRelative("typeName");
            if (typeNameProp != null) typeNameProp.stringValue = entry.TypeName;
            var enabledProp = prop.FindPropertyRelative("enabled");
            if (enabledProp != null) enabledProp.boolValue = entry.Enabled;
            var orderProp = prop.FindPropertyRelative("orderOverride");
            if (orderProp != null) orderProp.intValue = entry.OrderOverride;
        }

        // ── Refresh ──

        private void Refresh()
        {
            _unifiedList.itemsSource = _unifiedConfigs;
            _unifiedList.Rebuild();
        }
    }
}
