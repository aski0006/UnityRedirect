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
    /// 类型选择器工具。
    /// 扫描所有程序集中标记 [RouteData] 的 struct 类型，
    /// 提供 UI Toolkit DropdownField 供用户选择。
    /// </summary>
    public static class TypeSelector
    {
        private static List<Type> _cachedTypes;
        private static readonly string[] _builtInOptions =
        {
            "(Select a Route Data Type...)",
        };

        /// <summary>
        /// 获取所有标记 [RouteData] 的 struct 类型列表。
        /// </summary>
        public static IReadOnlyList<Type> GetRouteDataTypes()
        {
            if (_cachedTypes != null)
                return _cachedTypes;

            _cachedTypes = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                try
                {
                    foreach (var type in assembly.GetExportedTypes())
                    {
                        if (type.IsValueType &&
                            !type.IsEnum &&
                            type.IsDefined(typeof(RouteDataAttribute), false))
                        {
                            _cachedTypes.Add(type);
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip assemblies that can't be fully loaded
                }
            }

            return _cachedTypes;
        }

        /// <summary>
        /// 清空缓存，在重新编译后需调用。
        /// </summary>
        public static void ClearCache()
        {
            _cachedTypes = null;
        }

        /// <summary>
        /// 获取类型的选择列表选项（显示名称列表）。
        /// </summary>
        public static List<string> GetTypeDisplayNames()
        {
            var types = GetRouteDataTypes();
            var names = new List<string>(_builtInOptions);
            names.AddRange(types.Select(t => t.FullName));
            return names;
        }

        /// <summary>
        /// 根据选项索引获取对应的 Type。
        /// 索引 0 为占位符，返回 null。
        /// </summary>
        public static Type GetTypeByIndex(int index)
        {
            if (index <= 0)
                return null;

            var types = GetRouteDataTypes();
            var dataIndex = index - 1;
            return dataIndex < types.Count ? types[dataIndex] : null;
        }

        /// <summary>
        /// 获取类型完整名称在选项列表中的索引。
        /// 未找到返回 0（占位符）。
        /// </summary>
        public static int GetIndexByTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return 0;

            var names = GetTypeDisplayNames();
            for (var i = 1; i < names.Count; i++)
            {
                if (names[i] == fullName)
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// 创建一个类型选择的 DropdownField。
        /// </summary>
        public static DropdownField CreateTypeDropdown(
            string label,
            string initialValue,
            Action<Type> onTypeSelected)
        {
            var choices = GetTypeDisplayNames();
            var dropdown = new DropdownField(label, choices, 0);

            if (!string.IsNullOrEmpty(initialValue))
            {
                var idx = GetIndexByTypeName(initialValue);
                if (idx > 0)
                    dropdown.index = idx;
            }

            dropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = dropdown.index;
                var selectedType = GetTypeByIndex(idx);
                onTypeSelected?.Invoke(selectedType);
            });

            return dropdown;
        }
    }
}
