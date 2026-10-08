using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Guide.Editor
{
    /// <summary>
    /// [SubclassSelector] 的绘制器：给 [SerializeReference] 字段补上 Unity 缺失的"选具体类型"下拉框。
    /// 属性标在字段或 List 字段上即可（标在 List 上时，Unity 绘制元素会带着同一属性回到本绘制器，
    /// 机制与 [Range] 作用于 List&lt;float&gt; 每个元素相同）。
    /// 结构：托管引用 = 类型下拉一行 + 实例字段若干行；列表 = 自绘 foldout/Size/元素（元素即托管引用）。
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private const float LineGap = 2f;

        /// 类型下拉缓存：基类型 → 可选具体类型 + 菜单项。域重载时静态类重置，无需手动清理
        private class TypeMenu { public Type[] Types; public GUIContent[] Labels; }
        private static readonly Dictionary<Type, TypeMenu> MenuCache = new();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference)
                DrawReference(position, property, label);
            else if (property.isArray)
                DrawList(position, property, label);
            else
                EditorGUI.LabelField(position, label.text, "[SubclassSelector] 只支持 SerializeReference 字段或列表");
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                float h = EditorGUIUtility.singleLineHeight + LineGap; // 类型下拉行
                if (property.managedReferenceValue != null)
                    ForEachDirectChild(property, c =>
                    {
                        h += EditorGUI.GetPropertyHeight(c, null, true) + LineGap;
                        return true;
                    });
                return h;
            }
            if (property.isArray)
            {
                float h = EditorGUIUtility.singleLineHeight + LineGap; // foldout 行
                if (property.isExpanded)
                {
                    h += EditorGUIUtility.singleLineHeight + LineGap; // Size 行
                    for (int i = 0; i < property.arraySize; i++)
                        h += EditorGUI.GetPropertyHeight(property.GetArrayElementAtIndex(i), null, true) + LineGap;
                }
                return h;
            }
            return EditorGUIUtility.singleLineHeight;
        }

        // ── 托管引用：类型下拉 + 实例字段 ──
        private void DrawReference(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var menu = GetMenu(ElementType());
            int current = IndexOfCurrent(property, menu.Types);
            // 索引 0 = None（置空）；其余 = 具体类型
            int picked = EditorGUI.Popup(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                label, current + 1, menu.Labels);
            if (picked != current + 1)
                property.managedReferenceValue = picked == 0
                    ? null
                    : Activator.CreateInstance(menu.Types[picked - 1]);

            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++; // 实例字段缩进一级
                float y = position.y + EditorGUIUtility.singleLineHeight + LineGap;
                ForEachDirectChild(property, c =>
                {
                    float ch = EditorGUI.GetPropertyHeight(c, null, true);
                    EditorGUI.PropertyField(new Rect(position.x, y, position.width, ch), c, true);
                    y += ch + LineGap;
                    return true;
                });
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        // ── 列表：foldout + Size + 元素 ──
        // 元素是托管引用，PropertyField 会带着属性回到 DrawReference
        private void DrawList(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            property.isExpanded = EditorGUI.Foldout(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
                property.isExpanded, new GUIContent($"{label.text}  [{property.arraySize}]"), true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + LineGap;
                property.arraySize = EditorGUI.IntField(
                    new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight),
                    "Size", property.arraySize);
                y += EditorGUIUtility.singleLineHeight + LineGap;

                for (int i = 0; i < property.arraySize; i++)
                {
                    var element = property.GetArrayElementAtIndex(i);
                    float eh = EditorGUI.GetPropertyHeight(element, null, true);
                    // 右侧留出 ▲▼：命令按进入顺序执行、逆序回滚，顺序有语义，提供排序按钮
                    var rowRect = new Rect(position.x, y, position.width - 40f, eh);
                    EditorGUI.PropertyField(rowRect, element, new GUIContent($"Element {i}"), true);

                    var up = new Rect(position.xMax - 38f, y, 17f, EditorGUIUtility.singleLineHeight);
                    var down = new Rect(position.xMax - 19f, y, 17f, EditorGUIUtility.singleLineHeight);
                    using (new EditorGUI.DisabledScope(property.arraySize < 2))
                    {
                        if (GUI.Button(up, "▲") && i > 0) { property.MoveArrayElement(i, i - 1); break; }
                        if (GUI.Button(down, "▼") && i < property.arraySize - 1) { property.MoveArrayElement(i, i + 1); break; }
                    }
                    y += eh + LineGap;
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        // ── 工具 ──

        /// 遍历托管引用的直接子字段（只走同级；孙级由子字段自己的绘制器负责，如嵌套的 conditions 列表）
        private static void ForEachDirectChild(SerializedProperty parent, Func<SerializedProperty, bool> visit)
        {
            var it = parent.Copy();
            var end = it.GetEndProperty();
            if (!it.NextVisible(true) || SerializedProperty.EqualContents(it, end)) return;
            while (visit(it) && it.NextVisible(false) && !SerializedProperty.EqualContents(it, end)) { }
        }

        /// 字段声明类型 → 可选类型的基类型：List&lt;T&gt;/数组取元素类型，否则取自身
        private Type ElementType()
        {
            var t = fieldInfo.FieldType;
            if (t.IsArray) return t.GetElementType();
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>)) return t.GetGenericArguments()[0];
            return t;
        }

        private static TypeMenu GetMenu(Type baseType)
        {
            if (MenuCache.TryGetValue(baseType, out var cached)) return cached;
            // 与 SerializeReference 的要求一致：具体类 + [Serializable] + 无参构造
            var types = TypeCache.GetTypesDerivedFrom(baseType)
                .Where(t => !t.IsAbstract && !t.IsInterface
                            && t.IsDefined(typeof(SerializableAttribute), false)
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name).ToArray();
            var labels = new[] { new GUIContent("None") }
                .Concat(types.Select(t => new GUIContent(t.Name))).ToArray();
            var menu = new TypeMenu { Types = types, Labels = labels };
            MenuCache[baseType] = menu;
            return menu;
        }

        /// managedReferenceFullTypename 形如 "Assembly-CSharp Guide.AutoCompleteCondition"；空串 = 空引用。
        /// 类型已删除/改名时 Unity 会把引用置 null，这里显示为 None
        private static int IndexOfCurrent(SerializedProperty property, Type[] options)
        {
            string full = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(full)) return -1;
            for (int i = 0; i < options.Length; i++)
                if (full == $"{options[i].Assembly.GetName().Name} {options[i].FullName}")
                    return i;
            return -1;
        }
    }
}
