using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LatticeDeformerMultiMesh : MonoBehaviour
{
    public Bounds latticeBounds = new Bounds(Vector3.zero, Vector3.one);
    public Vector3Int resolution = new Vector3Int(3, 3, 3);
    public bool realtimeUpdate = true;

    // 存储每个子物体的原始数据
    private List<MeshFilter> meshFilters = new List<MeshFilter>();
    private List<Mesh> originalMeshes = new List<Mesh>();          // 原始网格引用
    private List<Vector3[]> originalVerticesList = new List<Vector3[]>(); // 顶点在子物体本地空间
    private List<Vector3[]> deformedVerticesList = new List<Vector3[]>();

    // 控制点相关
    private Transform controlPointRoot;
    private List<Transform> controlPoints = new List<Transform>();

    private void OnEnable()
    {
        CollectMeshFilters();
        StoreOriginalVertices();
    }

    private void Update()
    {
        if (!realtimeUpdate) return;
        if (controlPoints.Count == 0) return;
        if (originalVerticesList.Count == 0) return;

        DeformAllMeshes();
    }

    // 收集所有子物体（包括自身）的 MeshFilter
    private void CollectMeshFilters()
    {
        meshFilters.Clear();
        MeshFilter[] all = GetComponentsInChildren<MeshFilter>();
        foreach (var mf in all)
        {
            if (mf.sharedMesh != null)
                meshFilters.Add(mf);
        }
    }

    // 存储所有原始顶点（在子物体本地空间）及原始网格
    private void StoreOriginalVertices()
    {
        originalVerticesList.Clear();
        originalMeshes.Clear();
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh != null)
            {
                originalMeshes.Add(mf.sharedMesh);
                Vector3[] verts = mf.sharedMesh.vertices;
                originalVerticesList.Add(verts);
            }
        }
        // 初始化变形顶点数组
        deformedVerticesList.Clear();
        foreach (var verts in originalVerticesList)
        {
            deformedVerticesList.Add(new Vector3[verts.Length]);
        }
    }

    // 计算所有子物体在父物体本地空间的总包围盒
    public void FitBoundsToModel()
    {
        CollectMeshFilters();
        StoreOriginalVertices();

        if (meshFilters.Count == 0) return;

        bool first = true;
        Vector3 min = Vector3.zero, max = Vector3.zero;

        for (int i = 0; i < meshFilters.Count; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf.sharedMesh == null) continue;

            Vector3[] verts = mf.sharedMesh.vertices;
            foreach (Vector3 vert in verts)
            {
                // 将顶点从子物体本地空间转换到父物体本地空间
                Vector3 worldPos = mf.transform.TransformPoint(vert);
                Vector3 parentLocalPos = transform.InverseTransformPoint(worldPos);

                if (first)
                {
                    min = max = parentLocalPos;
                    first = false;
                }
                else
                {
                    min = Vector3.Min(min, parentLocalPos);
                    max = Vector3.Max(max, parentLocalPos);
                }
            }
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;
        latticeBounds = new Bounds(center, size * 1.2f); // 稍扩大一点
    }

    // 生成控制点（在父物体下）
    public void GenerateControlPoints()
    {
        CollectMeshFilters();
        StoreOriginalVertices();

        if (controlPointRoot != null)
        {
            if (Application.isPlaying)
                Destroy(controlPointRoot.gameObject);
            else
                DestroyImmediate(controlPointRoot.gameObject);
        }

        controlPointRoot = new GameObject("LatticeControlPoints").transform;
        controlPointRoot.SetParent(transform, false);
        controlPointRoot.localPosition = Vector3.zero;
        controlPointRoot.localRotation = Quaternion.identity;
        controlPointRoot.localScale = Vector3.one;

        controlPoints.Clear();
        int xRes = Mathf.Max(2, resolution.x);
        int yRes = Mathf.Max(2, resolution.y);
        int zRes = Mathf.Max(2, resolution.z);

        for (int i = 0; i < xRes; i++)
        {
            for (int j = 0; j < yRes; j++)
            {
                for (int k = 0; k < zRes; k++)
                {
                    float tx = i / (float)(xRes - 1);
                    float ty = j / (float)(yRes - 1);
                    float tz = k / (float)(zRes - 1);

                    Vector3 localPos = latticeBounds.min + new Vector3(
                        tx * latticeBounds.size.x,
                        ty * latticeBounds.size.y,
                        tz * latticeBounds.size.z
                    );

                    GameObject cp = new GameObject($"CP_{i}_{j}_{k}");
                    cp.transform.SetParent(controlPointRoot, false);
                    cp.transform.localPosition = localPos;

#if UNITY_EDITOR
                    cp.AddComponent<ControlPointGizmo>(); // 可复用之前的脚本
#endif

                    controlPoints.Add(cp.transform);
                }
            }
        }

        DeformAllMeshes();
    }

    // 对所有子物体执行变形
    public void DeformAllMeshes()
    {
        if (controlPoints.Count == 0 || meshFilters.Count == 0) return;

        int xRes = resolution.x;
        int yRes = resolution.y;
        int zRes = resolution.z;

        // 获取所有控制点的当前位置（父物体本地空间）
        Vector3[,,] currentPositions = new Vector3[xRes, yRes, zRes];
        int idx = 0;
        for (int i = 0; i < xRes; i++)
        {
            for (int j = 0; j < yRes; j++)
            {
                for (int k = 0; k < zRes; k++)
                {
                    currentPositions[i, j, k] = controlPoints[idx].localPosition;
                    idx++;
                }
            }
        }

        // 遍历每个子物体
        for (int m = 0; m < meshFilters.Count; m++)
        {
            MeshFilter mf = meshFilters[m];
            if (mf.sharedMesh == null) continue;

            Vector3[] origVerts = originalVerticesList[m];
            Vector3[] defVerts = deformedVerticesList[m];

            for (int v = 0; v < origVerts.Length; v++)
            {
                // 将原始顶点从子物体本地空间转换到父物体本地空间
                Vector3 worldPos = mf.transform.TransformPoint(origVerts[v]);
                Vector3 parentLocalPos = transform.InverseTransformPoint(worldPos);

                // 计算在晶格中的参数坐标 [0,1]
                Vector3 uv = new Vector3(
                    Mathf.InverseLerp(latticeBounds.min.x, latticeBounds.max.x, parentLocalPos.x),
                    Mathf.InverseLerp(latticeBounds.min.y, latticeBounds.max.y, parentLocalPos.y),
                    Mathf.InverseLerp(latticeBounds.min.z, latticeBounds.max.z, parentLocalPos.z)
                );
                uv.x = Mathf.Clamp01(uv.x);
                uv.y = Mathf.Clamp01(uv.y);
                uv.z = Mathf.Clamp01(uv.z);

                // 晶格单元索引
                int i = Mathf.FloorToInt(uv.x * (xRes - 1));
                int j = Mathf.FloorToInt(uv.y * (yRes - 1));
                int k = Mathf.FloorToInt(uv.z * (zRes - 1));
                i = Mathf.Clamp(i, 0, xRes - 2);
                j = Mathf.Clamp(j, 0, yRes - 2);
                k = Mathf.Clamp(k, 0, zRes - 2);

                float u = (uv.x * (xRes - 1)) - i;
                float vv = (uv.y * (yRes - 1)) - j;
                float w = (uv.z * (zRes - 1)) - k;

                // 三线性插值
                Vector3 p000 = currentPositions[i, j, k];
                Vector3 p001 = currentPositions[i, j, k + 1];
                Vector3 p010 = currentPositions[i, j + 1, k];
                Vector3 p011 = currentPositions[i, j + 1, k + 1];
                Vector3 p100 = currentPositions[i + 1, j, k];
                Vector3 p101 = currentPositions[i + 1, j, k + 1];
                Vector3 p110 = currentPositions[i + 1, j + 1, k];
                Vector3 p111 = currentPositions[i + 1, j + 1, k + 1];

                Vector3 p00 = Vector3.Lerp(p000, p001, w);
                Vector3 p01 = Vector3.Lerp(p010, p011, w);
                Vector3 p10 = Vector3.Lerp(p100, p101, w);
                Vector3 p11 = Vector3.Lerp(p110, p111, w);
                Vector3 p0 = Vector3.Lerp(p00, p01, vv);
                Vector3 p1 = Vector3.Lerp(p10, p11, vv);
                Vector3 newParentLocalPos = Vector3.Lerp(p0, p1, u);

                // 将新位置从父物体本地空间转换回子物体本地空间
                Vector3 newWorldPos = transform.TransformPoint(newParentLocalPos);
                Vector3 newChildLocalPos = mf.transform.InverseTransformPoint(newWorldPos);

                defVerts[v] = newChildLocalPos;
            }

            // 更新子物体的网格：判断当前网格是否还是原始网格（未变形过）
            if (mf.sharedMesh == originalMeshes[m])
            {
                // 首次变形：创建新网格实例
                Mesh newMesh = Instantiate(mf.sharedMesh);
                newMesh.vertices = defVerts;
                newMesh.RecalculateNormals();
                newMesh.RecalculateBounds();
                mf.sharedMesh = newMesh;
            }
            else
            {
                // 已有实例，直接更新顶点
                mf.sharedMesh.vertices = defVerts;
                mf.sharedMesh.RecalculateNormals();
                mf.sharedMesh.RecalculateBounds();
            }
        }
    }

    // 导出 Prefab（含所有子物体的变形网格）
    public void ExportAsPrefab(string path)
    {
#if UNITY_EDITOR
        // 确保网格最新
        DeformAllMeshes();

        // 复制整个层级结构
        GameObject exportRoot = Instantiate(gameObject);
        exportRoot.name = gameObject.name + "_Deformed";

        // 移除本组件
        LatticeDeformerMultiMesh deformer = exportRoot.GetComponent<LatticeDeformerMultiMesh>();
        if (deformer != null) DestroyImmediate(deformer);

        // 删除控制点根对象
        Transform cpRoot = exportRoot.transform.Find("LatticeControlPoints");
        if (cpRoot != null) DestroyImmediate(cpRoot.gameObject);

        // 获取所有 MeshFilter（包括子物体）
        MeshFilter[] allMF = exportRoot.GetComponentsInChildren<MeshFilter>();
        bool hasError = false;
        int meshIndex = 0;
        foreach (MeshFilter mf in allMF)
        {
            if (mf.sharedMesh == null) continue;

            Mesh sourceMesh = mf.sharedMesh;
            if (!sourceMesh.isReadable)
            {
                Debug.LogError($"网格 {sourceMesh.name} 不可读，无法导出变形网格。请在 FBX 导入设置中启用 Read/Write。");
                hasError = true;
                continue;
            }

            // 创建新网格
            Mesh newMesh = new Mesh();
            newMesh.name = sourceMesh.name + "_Deformed";

            newMesh.vertices = sourceMesh.vertices;
            newMesh.normals = sourceMesh.normals;
            newMesh.uv = sourceMesh.uv;
            newMesh.triangles = sourceMesh.triangles;

            if (sourceMesh.tangents != null && sourceMesh.tangents.Length > 0)
                newMesh.tangents = sourceMesh.tangents;
            if (sourceMesh.colors != null && sourceMesh.colors.Length > 0)
                newMesh.colors = sourceMesh.colors;
            if (sourceMesh.uv2 != null && sourceMesh.uv2.Length > 0)
                newMesh.uv2 = sourceMesh.uv2;

            newMesh.RecalculateBounds();

            // 保存为独立资产（使用路径 + 索引避免重名）
            string meshPath = System.IO.Path.ChangeExtension(path, null) + $"_Mesh_{meshIndex++}.asset";
            AssetDatabase.CreateAsset(newMesh, meshPath);
            AssetDatabase.SaveAssets();

            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        }

        if (hasError)
        {
            DestroyImmediate(exportRoot);
            return;
        }

        // 保存 Prefab
        PrefabUtility.SaveAsPrefabAsset(exportRoot, path);
        DestroyImmediate(exportRoot);

        Debug.Log($"Prefab 已导出到: {path}，同时生成 {meshIndex} 个网格资源文件。");
#endif
    }

    // 占位：导出 FBX 需要插件
    public void ExportAsFBX(string path)
    {
#if UNITY_EDITOR
        Debug.LogWarning("FBX 导出需要 Unity 的 FBX Exporter 插件，请手动导出或集成相关代码。");
#endif
    }
}