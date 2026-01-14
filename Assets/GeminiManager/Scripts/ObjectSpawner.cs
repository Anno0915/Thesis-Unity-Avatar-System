using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ObjectSpawner : MonoBehaviour
{
    // Inspectorでリスト表示するためのクラス定義
    [System.Serializable]
    public class SpawnableObject
    {
        [Tooltip("このオブジェクトを出現させるためのキーワードリスト (例: apple, ringo, fruit)")]
        public List<string> keywords;
        public GameObject prefab;
    }

    [Header("UI設定")]
    [Tooltip("オブジェクトを表示する吹き出しのCanvas（または親オブジェクト）")]
    public GameObject thoughtCanvas;

    [Header("生成オブジェクト設定")]
    [Tooltip("キーワードとPrefabの対応リスト")]
    public List<SpawnableObject> spawnableObjects;

    [Tooltip("オブジェクトの生成場所 (吹き出しの中央などを指定)")]
    public Transform spawnPoint;

    [Tooltip("オブジェクトの回転速度")]
    public float rotationSpeed = 50f;

    [Header("自動消去設定")]
    [Tooltip("オブジェクトが自動で消えるまでの秒数")]
    public float autoDestroyTime = 5.0f;

    // 内部変数
    private GameObject currentSpawnedObject; // 現在表示中のオブジェクト
    private Coroutine autoDestroyCoroutine;  // タイマー管理用

    /// <summary>
    /// 指定されたキーワードに一致するオブジェクトを生成する
    /// </summary>
    public void SpawnObject(string keyword)
    {
        // 1. 古いタイマーとオブジェクトの掃除
        if (autoDestroyCoroutine != null)
        {
            StopCoroutine(autoDestroyCoroutine);
            autoDestroyCoroutine = null;
        }

        if (currentSpawnedObject != null)
        {
            Destroy(currentSpawnedObject);
            currentSpawnedObject = null;
        }

        // キーワードが空なら、吹き出しを閉じて終了
        if (string.IsNullOrEmpty(keyword))
        {
            if (thoughtCanvas != null) thoughtCanvas.SetActive(false);
            return;
        }

        // 2. キーワード検索と生成
        // 入力されたキーワードを小文字化・空白削除して正規化
        string searchKey = keyword.ToLower().Trim();
        bool objectFound = false;

        foreach (var obj in spawnableObjects)
        {
            // リスト内のキーワードも小文字化して比較（大文字小文字を区別しない）
            // これにより "Apple" でも "apple" でもヒットするようになります
            foreach (string k in obj.keywords)
            {
                if (k.ToLower().Trim() == searchKey)
                {
                    // 一致したら生成
                    // 第2引数に spawnPoint を指定することで、生成されたオブジェクトを spawnPoint の「子」にします
                    // これにより、キャラクターが動いてもオブジェクトが追従します
                    if (spawnPoint != null)
                    {
                        currentSpawnedObject = Instantiate(obj.prefab, spawnPoint);

                        // 位置と回転をリセット（Prefabの設定を尊重しつつ、親の中心に配置）
                        currentSpawnedObject.transform.localPosition = Vector3.zero;
                        currentSpawnedObject.transform.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        // spawnPointがない場合のフォールバック（その場に生成）
                        currentSpawnedObject = Instantiate(obj.prefab, transform.position, Quaternion.identity);
                    }

                    objectFound = true;
                    break; // 内側のループを抜ける
                }
            }
            if (objectFound) break; // 外側のループも抜ける
        }

        // 3. 表示状態の更新
        if (thoughtCanvas != null)
        {
            thoughtCanvas.SetActive(objectFound);
        }

        // 見つかった場合のみ、自動消去タイマーを開始
        if (objectFound)
        {
            autoDestroyCoroutine = StartCoroutine(AutoDestroyObject());
        }
        else
        {
            Debug.Log($"ObjectSpawner: キーワード '{keyword}' に一致するオブジェクトは見つかりませんでした。");
        }
    }

    /// <summary>
    /// 指定時間後にオブジェクトと吹き出しを非表示にするコルーチン
    /// </summary>
    private IEnumerator AutoDestroyObject()
    {
        yield return new WaitForSeconds(autoDestroyTime);

        // オブジェクトを削除
        if (currentSpawnedObject != null)
        {
            Destroy(currentSpawnedObject);
            currentSpawnedObject = null;
        }

        // 吹き出しを非表示
        if (thoughtCanvas != null)
        {
            thoughtCanvas.SetActive(false);
        }

        autoDestroyCoroutine = null;
    }

    void Update()
    {
        // 表示中のオブジェクトをクルクル回す演出
        if (currentSpawnedObject != null)
        {
            currentSpawnedObject.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}
