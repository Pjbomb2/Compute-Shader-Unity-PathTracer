// #if UNITY_EDITOR
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEditor;
// using CommonVars;
// using UnityEditor.UIElements;
// using UnityEngine.UIElements;

// namespace TrueTrace {
//     [CustomEditor(typeof(ParentObject))]
//     public class ParentObjectEditor : Editor
//     {
//         public override bool RequiresConstantRepaint() => false;
//         public override VisualElement CreateInspectorGUI() {
//             var t1 = (targets);
//             var t =  t1[0] as ParentObject;
//             VisualElement Root = new VisualElement();
//             Toggle DeformableToggle = new Toggle() {value = t.IsDeformable, text = "Deformable Mesh"};
//             DeformableToggle.RegisterValueChangedCallback(evt => {t.IsDeformable = evt.newValue;});
//             Root.Add(DeformableToggle);
//             return Root;
//         }
//     }
// }
// #endif