using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class LatticeDeformer : MonoBehaviour
{
    public Bounds latticeBounds = new Bounds(Vector3.zero, Vector3.one);
    public Vector3Int resolution = new Vector3Int(3, 3, 3); // 控制点数量，最小2
    public bool realtimeUpdate = true;

    // 存储原始网格数据
    private Mesh originalMesh;
    private Vector3[] originalVertices;
    private Vector3[] deformedVertices;
    private MeshFilter meshFilter;
    private Mesh sharedMesh;

    // 控制点相关
    private Transform controlPointRoot;
    private List<Transform> controlPoints = new List<Transform>();
    private Vector3[,,] initialControlPointPositions; // 初始位置（模型空间）

    private void OnEnable()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            Debug.LogError("LatticeDeformer需要MeshFilter组件");
            enabled = false;
            return;
        }

        // 保存原始网格的副本，避免修改源文件
        if (originalMesh == null)
        {
            originalMesh = meshFilter.sharedMesh;
            if (originalMesh != null)
            {
                originalVertices = originalMesh.vertices;
                deformedVertices = new Vector3[originalVertices.Length];
                System.Array.Copy(originalVertices, deformedVertices, originalVertices.Length);
            }
        }
    }

    private void Update()
    {
        if (!realtimeUpdate) return;
        if (controlPoints.Count == 0) return;
        if (originalVertices == null) return;

        DeformMesh();
    }

    /// <summary>
    /// 生成控制点（作为子物体）
    /// </summary>
    public void GenerateControlPoints()
    {
        // 删除旧的控制点根对象
        if (controlPointRoot != null)
        {
            if (Application.isPlaying)
                Destroy(controlPointRoot.gameObject);
            else
                DestroyImmediate(controlPointRoot.gameObject);
        }

        // 创建新的根对象
        controlPointRoot = new GameObject("LatticeControlPoints").transform;
        controlPointRoot.SetParent(transform, false);
        controlPointRoot.localPosition = Vector3.zero;
        controlPointRoot.localRotation = Quaternion.identity;
        controlPointRoot.localScale = Vector3.one;

        controlPoints.Clear();
        int xRes = Mathf.Max(2, resolution.x);
        int yRes = Mathf.Max(2, resolution.y);
        int zRes = Mathf.Max(2, resolution.z);
        initialControlPointPositions = new Vector3[xRes, yRes, zRes];

        for (int i = 0; i < xRes; i++)
        {
            for (int j = 0; j < yRes; j++)
            {
                for (int k = 0; k < zRes; k++)
                {
                    // 计算标准化坐标 [0,1]
                    float tx = i / (float)(xRes - 1);
                    float ty = j / (float)(yRes - 1);
                    float tz = k / (float)(zRes - 1);

                    // 在晶格包围盒中定位
                    Vector3 localPos = latticeBounds.min + new Vector3(
                        tx * latticeBounds.size.x,
                        ty * latticeBounds.size.y,
                        tz * latticeBounds.size.z
                    );

                    // 创建控制点对象
                    GameObject cp = new GameObject($"CP_{i}_{j}_{k}");
                    cp.transform.SetParent(controlPointRoot, false);
                    cp.transform.localPosition = localPos;

                    // 添加可视化图标（可选）
#if UNITY_EDITOR
                    cp.AddComponent<ControlPointGizmo>();
#endif

                    controlPoints.Add(cp.transform);
                    initialControlPointPositions[i, j, k] = localPos;
                }
            }
        }

        // 初次变形
        DeformMesh();
    }

    /// <summary>
    /// 执行晶格变形
    /// </summary>
    public void DeformMesh()
    {
        if (originalVertices == null || controlPoints.Count == 0) return;

        int xRes = resolution.x;
        int yRes = resolution.y;
        int zRes = resolution.z;

        // 获取当前所有控制点的模型空间位置
        Vector3[,,] currentPositions = new Vector3[xRes, yRes, zRes];
        int index = 0;
        for (int i = 0; i < xRes; i++)
        {
            for (int j = 0; j < yRes; j++)
            {
                for (int k = 0; k < zRes; k++)
                {
                    currentPositions[i, j, k] = controlPoints[index].localPosition;
                    index++;
                }
            }
        }

        // 对每个顶点进行插值
        for (int v = 0; v < originalVertices.Length; v++)
        {
            Vector3 vert = originalVertices[v];

            // 计算顶点在晶格包围盒中的参数坐标 [0,1]
            Vector3 uv = new Vector3(
                Mathf.InverseLerp(latticeBounds.min.x, latticeBounds.max.x, vert.x),
                Mathf.InverseLerp(latticeBounds.min.y, latticeBounds.max.y, vert.y),
                Mathf.InverseLerp(latticeBounds.min.z, latticeBounds.max.z, vert.z)
            );

            // 钳位到[0,1]以避免晶格外部的顶点产生异常
            uv.x = Mathf.Clamp01(uv.x);
            uv.y = Mathf.Clamp01(uv.y);
            uv.z = Mathf.Clamp01(uv.z);

            // 计算所在的晶格单元索引
            int i = Mathf.FloorToInt(uv.x * (xRes - 1));
            int j = Mathf.FloorToInt(uv.y * (yRes - 1));
            int k = Mathf.FloorToInt(uv.z * (zRes - 1));

            // 边界保护
            i = Mathf.Clamp(i, 0, xRes - 2);
            j = Mathf.Clamp(j, 0, yRes - 2);
            k = Mathf.Clamp(k, 0, zRes - 2);

            // 单元内的局部坐标
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

            deformedVertices[v] = Vector3.Lerp(p0, p1, u);
        }

        // 更新网格
        if (meshFilter.sharedMesh != originalMesh)
        {
            // 如果已经替换为变形网格，直接更新顶点
            meshFilter.sharedMesh.vertices = deformedVertices;
            meshFilter.sharedMesh.RecalculateNormals();
            meshFilter.sharedMesh.RecalculateBounds();
        }
        else
        {
            // 首次变形：创建新网格实例
            Mesh deformedMesh = Instantiate(originalMesh);
            deformedMesh.vertices = deformedVertices;
            deformedMesh.RecalculateNormals();
            deformedMesh.RecalculateBounds();
            meshFilter.sharedMesh = deformedMesh;
        }
    }

    /// <summary>
    /// 导出变形后的 Prefab
    /// </summary>
    public void ExportAsPrefab(string path)
    {
#if UNITY_EDITOR
        // 确保网格是最新的
        DeformMesh();

        // 创建新物体
        GameObject exportObj = Instantiate(gameObject);
        exportObj.name = gameObject.name + "_Deformed";

        // 移除 LatticeDeformer 组件
        LatticeDeformer deformer = exportObj.GetComponent<LatticeDeformer>();
        if (deformer != null) DestroyImmediate(deformer);

        // 删除控制点根对象
        Transform cpRoot = exportObj.transform.Find("LatticeControlPoints");
        if (cpRoot != null) DestroyImmediate(cpRoot.gameObject);

        // 处理所有 MeshFilter（包括子物体）
        MeshFilter[] meshFilters = exportObj.GetComponentsInChildren<MeshFilter>();
        bool hasError = false;
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;

            Mesh sourceMesh = mf.sharedMesh;

            // 检查源网格是否可读
            if (!sourceMesh.isReadable)
            {
                Debug.LogError($"网格 {sourceMesh.name} 不可读，无法导出变形网格。请在 FBX 导入设置中启用 Read/Write。");
                hasError = true;
                continue;
            }

            // 创建一个全新的 Mesh 对象，并复制所有数据
            Mesh newMesh = new Mesh();
            newMesh.name = sourceMesh.name + "_Deformed";

            // 复制基本数据
            newMesh.vertices = sourceMesh.vertices;
            newMesh.normals = sourceMesh.normals;
            newMesh.uv = sourceMesh.uv;
            newMesh.triangles = sourceMesh.triangles;

            // 如有需要，复制其他属性（切线、顶点颜色、UV2等）
            if (sourceMesh.tangents != null && sourceMesh.tangents.Length > 0)
                newMesh.tangents = sourceMesh.tangents;
            if (sourceMesh.colors != null && sourceMesh.colors.Length > 0)
                newMesh.colors = sourceMesh.colors;
            if (sourceMesh.uv2 != null && sourceMesh.uv2.Length > 0)
                newMesh.uv2 = sourceMesh.uv2;

            newMesh.RecalculateBounds();

            // 关键步骤：将新网格保存为独立的 .asset 文件
            string meshPath = System.IO.Path.ChangeExtension(path, null) + "_Mesh.asset";
            AssetDatabase.CreateAsset(newMesh, meshPath);
            AssetDatabase.SaveAssets();

            // 在 MeshFilter 中引用这个网格资源
            mf.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        }

        if (hasError)
        {
            DestroyImmediate(exportObj);
            return;
        }

        // 保存 Prefab（此时网格已作为独立资源存在，Prefab 会正确引用）
        PrefabUtility.SaveAsPrefabAsset(exportObj, path);
        DestroyImmediate(exportObj);

        Debug.Log($"Prefab 已导出到: {path}，同时生成网格资源: {path}_Mesh.asset");
#endif
    }

    /// <summary>
    /// 导出为 FBX（需要 Unity FBX Exporter 插件）
    /// </summary>
    public void ExportAsFBX(string path)
    {
#if UNITY_EDITOR
        // 这里假设已安装 FBX Exporter，实际使用时请替换为您的导出逻辑
        Debug.LogWarning("FBX 导出需要 Unity 的 FBX Exporter 插件，请手动导出或集成相关代码。");
        // 示例：ModelExporter.ExportObject(path, meshFilter.sharedMesh);
#endif
    }

    /// <summary>
    /// 重置晶格范围适配模型的包围盒
    /// </summary>
    public void FitBoundsToModel()
    {
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Bounds meshBounds = meshFilter.sharedMesh.bounds;
        // 稍微扩大一点，使晶格完全包裹模型
        latticeBounds = new Bounds(meshBounds.center, meshBounds.size * 1.2f);
    }
}