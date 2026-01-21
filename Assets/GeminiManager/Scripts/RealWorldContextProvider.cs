using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;

//  OpenWeatherMap用 JSONデータ構造 
[Serializable]
public class WeatherInfo
{
    public Weather[] weather;
    public Main main;
    public string name;
}

[Serializable]
public class Weather
{
    public string main;        // Rain, Clear, Clouds etc.
    public string description; // light rain, etc.
}

[Serializable]
public class Main
{
    public float temp;         // 気温 (ケルビン単位の場合あり、設定による)
    public float humidity;     // 湿度
}

public class RealWorldContextProvider : MonoBehaviour
{
    [Header("OpenWeatherMap設定")]
    public string apiKey = "YOUR_API_KEY_HERE"; // ここにAPIキーを入力
    public string city = "Tokyo,jp"; // "Osaka,jp" のように指定

    [Header("更新設定")]
    public float updateInterval = 600f; // 10分ごとに更新

    // 現在の情報を保持する変数
    private string currentWeather = "Unknown";
    private float currentTemp = 0f;
    private bool isWeatherLoaded = false;

    // コルーチンの参照を保持して、リセットできるようにする
    private Coroutine weatherCoroutine;

    void Start()
    {
        // 定期的に天気を更新
        weatherCoroutine = StartCoroutine(UpdateWeatherRoutine());
    }

    // 外部から地域を変更するメソッド 
    /// <summary>
    /// 地域を変更して、即座に天気を更新する
    /// </summary>
    /// <param name="newCity">例: "Osaka,jp", "New York,us"</param>
    public void SetLocation(string newCity)
    {
        if (string.IsNullOrEmpty(newCity)) return;

        Debug.Log($"地域を {city} から {newCity} に変更します。");
        city = newCity;
        isWeatherLoaded = false; // 更新完了までフラグを下ろす

        // 既存の定期更新を停止して再起動
        if (weatherCoroutine != null) StopCoroutine(weatherCoroutine);
        weatherCoroutine = StartCoroutine(UpdateWeatherRoutine());
    }

    private IEnumerator UpdateWeatherRoutine()
    {
        while (true)
        {
            yield return GetWeatherFromAPI();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private IEnumerator GetWeatherFromAPI()
    {
        // metricを指定すると摂氏(℃)で取得できる
        string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric&lang=ja";

        using (UnityWebRequest www = new UnityWebRequest(url, "GET"))
        {
            www.downloadHandler = new DownloadHandlerBuffer();
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"天気取得エラー: {www.error}");
            }
            else
            {
                try
                {
                    WeatherInfo info = JsonUtility.FromJson<WeatherInfo>(www.downloadHandler.text);
                    if (info.weather.Length > 0)
                    {
                        currentWeather = info.weather[0].description; // "晴れ" "小雨" など
                        currentTemp = info.main.temp;
                        isWeatherLoaded = true;
                        Debug.Log($"天気更新: {currentWeather}, {currentTemp}℃");
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"天気JSONパースエラー: {e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Geminiに送るための「現在の状況」テキストを生成する
    /// </summary>
    public string GetContextString()
    {
        // 1. 時間の取得
        DateTime now = DateTime.Now;
        string timeStr = now.ToString("yyyy年MM月dd日 HH時mm分");

        // 時間帯の判定（挨拶などに影響させるため）
        string timeZone = "昼";
        if (now.Hour >= 5 && now.Hour < 11) timeZone = "朝";
        else if (now.Hour >= 17 && now.Hour < 20) timeZone = "夕方";
        else if (now.Hour >= 20 || now.Hour < 5) timeZone = "夜/深夜";

        // 2. 天気情報の構築
        string weatherStr = isWeatherLoaded
            ? $"天気: {currentWeather}, 気温: {currentTemp:F1}度"
            : "天気: 情報取得中";

        // 3. プロンプト用テキストの結合
        // AIに対して「今はこういう状況だよ」と教えるフォーマット
        return $"\n[現在の現実世界情報]\n現在時刻: {timeStr} ({timeZone})\n{weatherStr}\n";
    }
}
