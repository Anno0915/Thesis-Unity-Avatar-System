using UnityEngine;

public class CameraOrbit : MonoBehaviour
{
    [Header("ターゲット設定")]
    [Tooltip("中心となる対象（Headボーンを入れると確実です）")]
    public Transform target;

    [Header("距離とズーム")]
    [Tooltip("対象との現在の距離")]
    public float distance = 2.5f; // 初期値を少し離す
    [Tooltip("ズームの速さ")]
    public float zoomSpeed = 2.0f;

    [Tooltip("最小距離（これ以上近づけない）。0.5以上推奨")]
    public float minDistance = 0.6f; // 頭にめり込まないための安全距離

    [Tooltip("最大距離（これ以上離れない）")]
    public float maxDistance = 10.0f;

    [Header("回転設定")]
    [Tooltip("マウス感度")]
    public float sensitivity = 5.0f;
    [Tooltip("上下の回転制限（最小角度）")]
    public float yMinLimit = -20f;
    [Tooltip("上下の回転制限（最大角度）")]
    public float yMaxLimit = 80f;

    [Header("位置調整")]
    [Tooltip("高さのオフセット（TargetにHeadを入れた場合は0、足元の場合は1.4くらい）")]
    public float heightOffset = 0.0f; // Headボーン指定を想定して0をデフォルトに

    [Header("滑らかさ")]
    [Tooltip("回転の滑らかさ")]
    public float rotationSmoothTime = 0.12f;

    // 内部変数
    private float rotationY;
    private float rotationX;
    private float currentRotationX;
    private float currentRotationY;
    private float xVelocity;
    private float yVelocity;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.y;
        rotationY = angles.x;

        currentRotationX = rotationX;
        currentRotationY = rotationY;

        // ターゲット自動検索
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // Playerタグのオブジェクトが見つかったら、可能ならHeadを探す
                Transform head = player.transform.Find("Character1_Reference/Character1_Head"); // Unityちゃん構造の例
                if (head != null)
                {
                    target = head;
                    heightOffset = 0f; // Headが見つかったのでオフセット不要
                }
                else
                {
                    target = player.transform;
                    heightOffset = 1.4f; // Headが見つからないので足元+オフセット
                }
            }
        }

        // 開始時に距離が近すぎないかチェックして補正
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 入力（右クリック回転）
        if (Input.GetMouseButton(1))
        {
            rotationX += Input.GetAxis("Mouse X") * sensitivity;
            rotationY -= Input.GetAxis("Mouse Y") * sensitivity;
            rotationY = Mathf.Clamp(rotationY, yMinLimit, yMaxLimit);
        }

        // 2. ズーム（ホイール）
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;

        // 【重要】ここで最小距離(minDistance)を下回らないように制限します
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 3. 滑らか移動
        currentRotationX = Mathf.SmoothDamp(currentRotationX, rotationX, ref xVelocity, rotationSmoothTime);
        currentRotationY = Mathf.SmoothDamp(currentRotationY, rotationY, ref yVelocity, rotationSmoothTime);

        // 4. 座標計算
        Vector3 focusPoint = target.position + Vector3.up * heightOffset;
        Quaternion rotation = Quaternion.Euler(currentRotationY, currentRotationX, 0);

        // ターゲットから distance 分だけ後ろに配置
        Vector3 position = focusPoint - (rotation * Vector3.forward * distance);

        // 5. 適用
        transform.rotation = rotation;
        transform.position = position;
    }
}
