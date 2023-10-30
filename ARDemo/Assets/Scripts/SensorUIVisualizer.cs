using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MongoDB.Bson;
using MongoDB.Driver;

public class SensorUIVisualizer : MonoBehaviour
{

    public Sensor sensor;

    public Text MacText;

    public Text StatusText;

    public Text BatteryText;

    public Text SignalText;

    public Text TemperatureText;

    public Text HumidityText;

    public Button CloseButton;

    public Button UploadButton;

    private bool sensorExist = true;

    // Use this for initialization
    void Start()
    {
        CloseButton.onClick.AddListener(OnCloseButtonOnClick);
        UploadButton.onClick.AddListener(OnUploadButtonOnClick);
    }

    // Update is called once per frame
    void Update()
    {
        if (sensorExist)
        {
            if (sensor != null)
            {
                MacText.text += ":" + sensor.Mac;
                StatusText.text += ":" + (sensor.Status == 0 ? "online" : "offline");
                BatteryText.text += ":" + sensor.Battery.ToString() + "%";
                SignalText.text += ":" + sensor.Signal.ToString() + "dbm";
                TemperatureText.text += ":" + sensor.Temperature.ToString() + "℃";
                HumidityText.text += ":" + sensor.Humidity.ToString() + "%";
				sensorExist = false;
            }
        }



    }

    void OnDestroy()
    {
        CloseButton.onClick.RemoveListener(OnCloseButtonOnClick);
        UploadButton.onClick.RemoveListener(OnUploadButtonOnClick);
    }

    private void OnCloseButtonOnClick()
    {
        GameObject.Destroy(this.transform.parent.gameObject);
    }

    private void OnUploadButtonOnClick()
    {
        var client = new MongoClient("mongodb://192.168.43.142:27017");

        var database = client.GetDatabase("test");

        var collectoin = database.GetCollection<BsonDocument>("sensor");

        var document = new BsonDocument {
            {"mac", sensor.Mac},
            {"status", sensor.Status},
            {"signal", sensor.Signal},
            {"temperature", sensor.Temperature},
            {"humidity", sensor.Humidity},
            {"last_update_time", sensor.Last_update_time},
            {"upload_time", System.DateTime.Now.ToString()}
        };

        collectoin.InsertOne(document);

        _ShowAndroidToastMessage("数据保存成功");
    }

    private void _ShowAndroidToastMessage(string message)
    {
        AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject unityActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        if (unityActivity != null)
        {
            AndroidJavaClass ToastClass = new AndroidJavaClass("android.widget.Toast");
            unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
            {
                AndroidJavaObject toastObject = ToastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
                    message, 0);
                toastObject.Call("show");
            }));
        }
    }

}
