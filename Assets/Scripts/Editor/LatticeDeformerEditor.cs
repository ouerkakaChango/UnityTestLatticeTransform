using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(LatticeDeformer))]
public class LatticeDeformerEditor : Editor
{
    private LatticeDeformer deformer;

    private void OnEnable()
    {
        deformer = target as LatticeDeformer;
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
            deformer.DeformMesh();
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

        // 刷新场景视图，使绘制的线框实时更新
        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        if (deformer == null) return;

        // 设置矩阵为物体的本地到世界变换，使绘制的线框跟随物体的位置/旋转/缩放
        Handles.matrix = deformer.transform.localToWorldMatrix;
        Handles.color = Color.green;
        Handles.DrawWireCube(deformer.latticeBounds.center, deformer.latticeBounds.size);
        // 恢复矩阵
        Handles.matrix = Matrix4x4.identity;
    }
}