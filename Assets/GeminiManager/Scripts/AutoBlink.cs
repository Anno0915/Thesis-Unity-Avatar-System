using UnityEngine;
using System.Collections;

public class AutoBlink : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("オート目パチを有効にするか")]
    public bool isActive = true;

    [Header("メッシュ設定 (重要)")]
    [Tooltip("目のメッシュ (EYE_DEF または MTH_DEF)")]
    public SkinnedMeshRenderer ref_SMR_EYE_DEF;

    [Tooltip("まつ毛のメッシュ (EL_DEF) ※なければ空欄でOK")]
    public SkinnedMeshRenderer ref_SMR_EL_DEF;

    [Header("BlendShape設定")]
    [Tooltip("まばたきパラメータの番号 (上から何番目か。0からスタート)")]
    public int blendShapeIndex = 6;

    [Header("詳細設定")]
    [Tooltip("目を閉じた時のBlendShapeの値 (通常100か85)")]
    public float ratio_Close = 85.0f;

    [Tooltip("半目の時のBlendShapeの値")]
    public float ratio_HalfClose = 20.0f;

    [HideInInspector]
    public float ratio_Open = 0.0f; // 開いている時は0

    [Tooltip("まばたき1回にかかる時間 (秒)")]
    public float timeBlink = 0.4f;

    [Tooltip("ランダム判定の閾値 (0〜1)。大きいほどまばたきしにくくなる")]
    public float threshold = 0.3f;

    [Tooltip("まばたき判定を行う間隔 (秒)")]
    public float interval = 3.0f;

    // 内部クラス：まばたきのアニメーション状態管理
    class EyelidAnimator
    {
        private float timeBlinkSec;     // 1ステートあたりの所要時間
        private float timeRemaining = 0f; // 残り時間

        private SkinnedMeshRenderer ref_SMR_EYE_DEF;
        private SkinnedMeshRenderer ref_SMR_EL_DEF;
        private int index; // 制御するBlendShapeのインデックス

        // まばたきの状態定義
        enum Status
        {
            Open = 0,       // 開いている
            HalfClose,      // 閉じかけ（半目）
            Close,          // 閉じている
            HalfOpen,       // 開きかけ（半目）
            NotAnimating,   // アニメーションしていない
            STATUS_LENGTH
        }
        private Status eyeStatus;

        // 状態遷移のデータ構造
        struct StateTransition
        {
            public Status NextState;    // 次の状態
            public float StartWeight;   // 開始時のウェイト
            public float EndWeight;     // 終了時のウェイト
        }
        StateTransition[] stateTable = new StateTransition[(int)Status.STATUS_LENGTH];

        public EyelidAnimator(SkinnedMeshRenderer eyeDef, SkinnedMeshRenderer elDef, int idx)
        {
            ref_SMR_EYE_DEF = eyeDef;
            ref_SMR_EL_DEF = elDef;
            index = idx;

            // 状態遷移テーブルの構築
            // Open -> HalfClose -> Close -> HalfOpen -> NotAnimating
            stateTable[0].NextState = Status.HalfClose;
            stateTable[1].NextState = Status.Close;
            stateTable[2].NextState = Status.HalfOpen;
            stateTable[3].NextState = Status.NotAnimating;
        }

        // まばたき開始
        public void Start(float timeBlinkSec, float ratioClose, float ratioHalfClose, float ratioOpen)
        {
            // 全体の時間を4分割して、各ステートの時間とする
            this.timeBlinkSec = timeBlinkSec / 4f;
            timeRemaining = this.timeBlinkSec;

            eyeStatus = Status.Open;

            // 各ステートでのウェイト変化を設定
            // Open -> HalfClose
            stateTable[0].StartWeight = ratioOpen;
            stateTable[0].EndWeight = ratioHalfClose;

            // HalfClose -> Close
            stateTable[1].StartWeight = ratioHalfClose;
            stateTable[1].EndWeight = ratioClose;

            // Close -> HalfOpen
            stateTable[2].StartWeight = ratioClose;
            stateTable[2].EndWeight = ratioHalfClose;

            // HalfOpen -> Open (NotAnimatingへ)
            stateTable[3].StartWeight = ratioHalfClose;
            stateTable[3].EndWeight = ratioOpen;
        }

        // BlendShapeの値を適用する
        private void setRatio(float ratio)
        {
            // EYE_DEFのチェックと適用
            if (ref_SMR_EYE_DEF != null)
            {
                if (index >= 0 && index < ref_SMR_EYE_DEF.sharedMesh.blendShapeCount)
                {
                    ref_SMR_EYE_DEF.SetBlendShapeWeight(index, ratio);
                }
            }

            // EL_DEF（まつ毛）のチェックと適用
            if (ref_SMR_EL_DEF != null)
            {
                if (index >= 0 && index < ref_SMR_EL_DEF.sharedMesh.blendShapeCount)
                {
                    ref_SMR_EL_DEF.SetBlendShapeWeight(index, ratio);
                }
            }
        }

        // 毎フレーム更新処理
        public void Update()
        {
            if (!IsAnimating()) return;

            timeRemaining -= Time.deltaTime;

            // 残り時間から進行度(0.0〜1.0)を計算
            var animWeight = 1f - Mathf.Clamp(timeRemaining / this.timeBlinkSec, 0, 1);

            var stateData = stateTable[(int)eyeStatus];

            // 時間切れなら次のステートへ
            if (timeRemaining < 0f)
            {
                eyeStatus = stateData.NextState;
                timeRemaining += timeBlinkSec;
            }

            // 現在のウェイトを計算して適用
            var ratio = Mathf.Lerp(stateData.StartWeight, stateData.EndWeight, animWeight);
            setRatio(ratio);
        }

        public bool IsAnimating()
        {
            return eyeStatus != Status.NotAnimating;
        }
    }

    private EyelidAnimator eyelidAnimator;

    void Start()
    {
        // 設定漏れの警告
        if (ref_SMR_EYE_DEF == null)
        {
            Debug.LogWarning("AutoBlink: 目のメッシュ(ref_SMR_EYE_DEF)が設定されていません。", this);
        }

        // アニメーターの初期化
        eyelidAnimator = new EyelidAnimator(ref_SMR_EYE_DEF, ref_SMR_EL_DEF, blendShapeIndex);

        // ランダム判定用コルーチン開始
        // 文字列指定("RandomChange")ではなく、メソッド呼び出しに変更（安全性向上）
        StartCoroutine(RandomChange());
    }

    // Animatorがポーズを上書きした後で実行するためにLateUpdateを使用
    void LateUpdate()
    {
        if (isActive && eyelidAnimator != null)
        {
            eyelidAnimator.Update();
        }
    }

    // ランダム判定用コルーチン
    IEnumerator RandomChange()
    {
        while (true)
        {
            // 次の判定までインターバルを置く
            yield return new WaitForSeconds(interval);

            // アニメーション中でなければランダム判定
            if (!eyelidAnimator.IsAnimating())
            {
                float _seed = Random.Range(0.0f, 1.0f);
                if (_seed > threshold)
                {
                    eyelidAnimator.Start(timeBlink, ratio_Close, ratio_HalfClose, ratio_Open);
                }
            }
        }
    }
}
