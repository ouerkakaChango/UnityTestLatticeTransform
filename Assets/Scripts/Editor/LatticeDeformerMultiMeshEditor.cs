using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LatticeDeformerMultiMesh))]
public class LatticeDeformerMultiMeshEditor : Editor
{
    private LatticeDeformerMultiMesh deformer;

    private void OnEnable()
    {
        deformer = target as LatticeDeformerMultiMesh;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("latticeBounds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("resolution"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("realtimeUpdate"));

        GUILayout.Space(10);

        if (GUILayout.Button("适配模型范围"))
        {
            deformer.FitBoundsToModel();
            EditorUtility.SetDirty(deformer);
        }

        if (GUILayout.Button("生成控制点"))
        {
            deformer.GenerateControlPoints();
            EditorUtility.SetDirty(deformer);
        }

        if (GUILayout.Button("手动更新变形"))
        {
            deformer.DeformAllMeshes();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("导出变形 Prefab"))
        {
            string path = EditorUtility.SaveFilePanelInProject("保存 Prefab", deformer.name + "_Deformed", "prefab", "保存变形后的 Prefab");
            if (!string.IsNullOrEmpty(path))
            {
                deformer.ExportAsPrefab(path);
            }
        }

        if (GUILayout.Button("导出 FBX (需插件)"))
        {
            string path = EditorUtility.SaveFilePanel("导出 FBX", Application.dataPath, deformer.name + "_Deformed", "fbx");
            if (!string.IsNullOrEmpty(path))
            {
                deformer.ExportAsFBX(path);
            }
        }

        serializedObject.ApplyModifiedProperties();

        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        if (deformer == null) return;

        // 使晶格线框跟随根物体变换
        Handles.matrix = deformer.transform.localToWorldMatrix;
        Handles.color = Color.green;
        Handles.DrawWireCube(deformer.latticeBounds.center, deformer.latticeBounds.size);
        Handles.matrix = Matrix4x4.identity;
    }
}