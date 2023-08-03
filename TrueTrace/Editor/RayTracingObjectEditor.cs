using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace TrueTrace {
    [CustomEditor(typeof(RayTracingObject)), CanEditMultipleObjects]
    public class RayTracingObjectEditor : Editor
    {

        int Selected = 0;
        string[] TheseNames;
        void OnEnable()
        {
            (target as RayTracingObject).matfill();
        }

        public override void OnInspectorGUI() {
                var t1 = (targets);
                var t =  t1[0] as RayTracingObject;
                TheseNames = t.Names;
                Selected = EditorGUILayout.Popup("Selected Material:", Selected, TheseNames);
                EditorGUILayout.Space();
                EditorGUI.BeginChangeCheck();
                t.MaterialOptions[Selected] = (RayTracingObject.Options)EditorGUILayout.EnumPopup("MaterialType: ", t.MaterialOptions[Selected]);
                Color BaseCol = EditorGUILayout.ColorField("Base Color", new Color(t.BaseColor[Selected].x, t.BaseColor[Selected].y, t.BaseColor[Selected].z, 1));
                serializedObject.FindProperty("BaseColor").GetArrayElementAtIndex(Selected).vector3Value = new Vector3(BaseCol.r, BaseCol.g, BaseCol.b);
                serializedObject.FindProperty("emmission").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.FloatField("Emission: ", t.emmission[Selected]);
                serializedObject.FindProperty("EmissionColor").GetArrayElementAtIndex(Selected).vector3Value = EditorGUILayout.Vector3Field("Emission Color: ", t.EmissionColor[Selected]);
                serializedObject.FindProperty("Roughness").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Roughness: ", t.Roughness[Selected], 0, 1);
                serializedObject.FindProperty("IOR").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("IOR: ", t.IOR[Selected], 0, 10);
                serializedObject.FindProperty("Metallic").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Metallic: ", t.Metallic[Selected], 0, 1);
                serializedObject.FindProperty("Specular").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Specular: ", t.Specular[Selected], 0, 1);
                serializedObject.FindProperty("SpecularTint").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Specular Tint: ", t.SpecularTint[Selected], 0, 1);
                serializedObject.FindProperty("Sheen").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Sheen: ", t.Sheen[Selected], 0, 10);
                serializedObject.FindProperty("SheenTint").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Sheen Tint: ", t.SheenTint[Selected], 0, 1);
                serializedObject.FindProperty("ClearCoat").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Clearcoat: ", t.ClearCoat[Selected], 0, 1);
                serializedObject.FindProperty("ClearCoatGloss").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Clearcoat Gloss: ", t.ClearCoatGloss[Selected], 0, 1);
                serializedObject.FindProperty("Anisotropic").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Anisotropic: ", t.Anisotropic[Selected], 0, 1);
                serializedObject.FindProperty("SpecTrans").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Specular Transmission: ", t.SpecTrans[Selected], 0, 1);
                serializedObject.FindProperty("DiffTrans").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Diffuse Transmission: ", t.DiffTrans[Selected], 0, 1);
                serializedObject.FindProperty("TransmissionColor").GetArrayElementAtIndex(Selected).vector3Value = EditorGUILayout.Vector3Field("Transmission Color: ", t.TransmissionColor[Selected]);
                serializedObject.FindProperty("Flatness").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Flatness: ", t.Flatness[Selected], 0, 1);
                serializedObject.FindProperty("Thin").GetArrayElementAtIndex(Selected).intValue = EditorGUILayout.IntField("Thin: ", t.Thin[Selected]);
                serializedObject.FindProperty("ScatterDist").GetArrayElementAtIndex(Selected).floatValue = EditorGUILayout.Slider("Scatter Distance: ", t.ScatterDist[Selected], 0, 5);
                if(EditorGUI.EndChangeCheck()) {
                    for(int i = 0; i < t1.Length; i++) {
                        (t1[i] as RayTracingObject).CallMaterialEdited();

                    }
                }
                // for(int i = 0; i < t1.Length; i++) {
                    // (t1[i] as RayTracingObject).CallMaterialOverride();
                    
                // }
                serializedObject.FindProperty("FollowMaterial").GetArrayElementAtIndex(Selected).boolValue = EditorGUILayout.Toggle("Link Mat To Unity Material: ", t.FollowMaterial[Selected]);
                serializedObject.ApplyModifiedProperties();
                if(GUILayout.Button("Force Update Materials")) {
                    t.CallMaterialEdited();
                }
        }
    }
}