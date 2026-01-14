using UnityEngine;
using System.Collections.Generic; // Listを使うために追加
using System.Linq; // 平均値計算のために追加

public class VolumeLipSync : MonoBehaviour
{
    [Header("設定")]
    public AudioSource audioSource;
    public SkinnedMeshRenderer faceMesh;
    [Tooltip("口パクで操作するBlendShape名 (例: A, aa, MouthOpen)")]
    public string lipSyncBlendShape = "A";

    [Header("調整")]
    [Tooltip("感度: 音量をBlendShapeの値に変換する倍率")]
    public float sensitivity = 100.0f;
    [Tooltip("口の開きの最大値 (0-100)")]
    public float maxMouthOpen = 100.0f;
    [Tooltip("滑らかさ: 値が大きいほどキビキビ動く")]
    public float smoothFactor = 20.0f;

    [Header("デバッグ・検証")]
    public bool showDebugLog = true;
    [Tooltip("検証モード: ONにすると発話終了時に相関係数を計算してログに出します")]
    public bool analyzeAccuracy = true;

    private int blendShapeIndex = -1;
    private float currentMouthOpen = 0.0f;

    // メモリ確保最適化用
    private float[] samples = new float[256];

    // 検証用データ蓄積リスト
    private List<float> rmsHistory = new List<float>();
    private List<float> blendShapeHistory = new List<float>();
    private bool wasPlaying = false;

    void Start()
    {
        if (faceMesh == null)
        {
            Debug.LogError("【エラー】Face Mesh が設定されていません！");
            return;
        }

        blendShapeIndex = faceMesh.sharedMesh.GetBlendShapeIndex(lipSyncBlendShape);

        if (blendShapeIndex == -1)
        {
            Debug.LogError($"【エラー】BlendShape名 '{lipSyncBlendShape}' が見つかりません！");
        }
    }

    void LateUpdate()
    {
        if (audioSource == null || faceMesh == null || blendShapeIndex == -1) return;

        float targetOpen = 0.0f;
        float currentRms = 0.0f;

        bool isPlaying = audioSource.isPlaying;

        if (isPlaying)
        {
            // 音声データを取得
            audioSource.GetOutputData(samples, 0);

            // RMS計算
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }
            currentRms = Mathf.Sqrt(sum / samples.Length);

            // 感度を掛けてクランプ
            targetOpen = Mathf.Clamp(currentRms * sensitivity, 0, maxMouthOpen);
        }

        // 滑らかに補間
        currentMouthOpen = Mathf.Lerp(currentMouthOpen, targetOpen, Time.deltaTime * smoothFactor);

        // 適用
        faceMesh.SetBlendShapeWeight(blendShapeIndex, currentMouthOpen);

        // ▼▼▼ 検証用ロジック ▼▼▼
        if (analyzeAccuracy)
        {
            // 再生中ならデータを記録
            if (isPlaying)
            {
                rmsHistory.Add(currentRms);
                blendShapeHistory.Add(currentMouthOpen);
            }
            // 再生終了を検知（立ち下がりエッジ）
            else if (wasPlaying && !isPlaying)
            {
                CalculateAndLogCorrelation();
                // リストをクリアして次の発話に備える
                rmsHistory.Clear();
                blendShapeHistory.Clear();
            }
        }

        wasPlaying = isPlaying;
    }

    // 相関係数を計算してログに出すメソッド
    private void CalculateAndLogCorrelation()
    {
        int count = rmsHistory.Count;
        if (count < 10) return; // データが少なすぎる場合は無視

        float avgRms = rmsHistory.Average();
        float avgShape = blendShapeHistory.Average();

        float sumCovariance = 0f; // 共分散の分子
        float sumSqrDiffRms = 0f; // RMSの分散の分子
        float sumSqrDiffShape = 0f; // Shapeの分散の分子

        for (int i = 0; i < count; i++)
        {
            float diffRms = rmsHistory[i] - avgRms;
            float diffShape = blendShapeHistory[i] - avgShape;

            sumCovariance += diffRms * diffShape;
            sumSqrDiffRms += diffRms * diffRms;
            sumSqrDiffShape += diffShape * diffShape;
        }

        // 分母が0になるのを防ぐ
        if (sumSqrDiffRms <= 0 || sumSqrDiffShape <= 0)
        {
            Debug.LogWarning("[LipSync Analysis] データ分散なし（ずっと無音か、口が動いていません）");
            return;
        }

        // 相関係数の計算
        float correlation = sumCovariance / Mathf.Sqrt(sumSqrDiffRms * sumSqrDiffShape);

        // 結果出力
        Debug.Log($"[LipSync Analysis] サンプル数: {count}, 相関係数: {correlation:F4} (1.0に近いほど高精度)");
    }
}
