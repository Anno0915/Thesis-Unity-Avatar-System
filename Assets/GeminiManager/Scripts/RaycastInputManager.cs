using UnityEngine;

public class RaycastInputManager : MonoBehaviour
{
    [Header("入力モード設定")]
    public bool useLaserPointer = false;
    public Transform laserOrigin;

    [Header("参照")]
    public TouchReactionHandler reactionHandler;

    void Update()
    {
        // クリック（タッチ）された瞬間
        if (Input.GetMouseButtonDown(0))
        {
            ProcessRaycast();
        }
    }

    private void ProcessRaycast()
    {
        Ray ray;
        if (useLaserPointer)
        {
            Transform origin = laserOrigin != null ? laserOrigin : Camera.main.transform;
            ray = new Ray(origin.position, origin.forward);
        }
        else
        {
            // カメラがタグ付けされているか確認
            if (Camera.main == null)
            {
                Debug.LogError("【エラー】MainCameraが見つかりません。カメラのTagを'MainCamera'にしてください。");
                return;
            }
            ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        }

        // デバッグ用：クリックした方向に赤い線を3秒間表示する
        Debug.DrawRay(ray.origin, ray.direction * 700, Color.red, 3.0f);

        RaycastHit hit;
        // 距離を100mに伸ばして確認
        if (Physics.Raycast(ray, out hit, 700.0f))
        {
            // 何かに当たったら、その名前をログに出す
            Debug.Log($"Raycastが当たったオブジェクト: {hit.collider.gameObject.name}");

            BodyPartTag bodyPart = hit.collider.GetComponent<BodyPartTag>();

            if (bodyPart != null)
            {
                Debug.Log($"部位判定成功: {bodyPart.partType}");
                if (reactionHandler != null)
                {
                    reactionHandler.OnBodyPartTouched(bodyPart.partType);
                }
            }
            else
            {
                Debug.LogWarning($"【失敗】当たりましたが、BodyPartTagがついていません。邪魔をしている可能性があります。");
            }
        }
        else
        {
            Debug.Log("Raycastは誰にも当たりませんでした。");
        }
    }
}
