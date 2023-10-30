using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LitJson;
using GoogleARCore;
using GoogleARCore.Examples.Common;
using ZXing;
using System;
using UnityEngine.UI;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading;

public class AppController : MonoBehaviour {

	public Camera FirstPersonCamera;

	public SensorUIVisualizer SensorUIVisualizerPrefab;

	private bool m_IsQuitting = false;

	private Sensor sensor;

	private bool textHasInstantiated = false;

	private Texture2D screenTexture;

	private Color32[] data;

	private BarcodeReader barcodeReader;

	private float scanInterval = 3.0f;

	public GameObject PlaneGenerator;

	private string sensorMac;

	public GameObject SnackBar;

	public Text SnackBarText;

	public GameObject ScanOverlay;

	public RawImage HandAnimation;
	
	// Use this for initialization
	void Start () {

		screenTexture = new Texture2D(Screen.width, Screen.height);

		barcodeReader = new BarcodeReader();

		PlaneGenerator.SetActive(false);

		SnackBar.SetActive(true);

		SnackBarText.text = "请将二维码对准扫描框进行扫描";

		ScanOverlay.SetActive(true);

	}
	
	// Update is called once per frame
	void Update () {

		_UpdateApplicationLifecycle();

		// 二维码扫描模块
		if (sensorMac == null) {
			// 二维码扫描计算器随时间增加
			scanInterval += Time.deltaTime;
			
			screenTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
			screenTexture.Apply();

			// 每3秒扫描一次二维码
			if (scanInterval > 3.0f) {
				// 获取传感器mac型号
				sensorMac = ScanQRCode();

				if (sensorMac != null) {
					// 请求云平台接口获得温湿度数据
					StartCoroutine(RequestSensorData(sensorMac));
				}

				// 二维码扫描计时器清零
				scanInterval = 0.0f;
			}
		}

		// 寻找平面并放置模型模块
		else {

			Touch touch;
			if (Input.touchCount < 1 || (touch = Input.GetTouch(0)).phase != TouchPhase.Began) {
				return;
			}

			TrackableHit hit;
			TrackableHitFlags raycastFilter = TrackableHitFlags.PlaneWithinPolygon;

			if (Frame.Raycast(touch.position.x, touch.position.y, raycastFilter, out hit)) {
				if ((hit.Trackable is DetectedPlane) &&
					Vector3.Dot(FirstPersonCamera.transform.position - hit.Pose.position,
						hit.Pose.rotation * Vector3.up) < 0) {
							_ShowAndroidToastMessage("点击在了检测到的平面的背面");
						}
				else {
					// _ShowAndroidToastMessage("点击在了检测到的平面上");

					// 模型还未实例化
					if (!textHasInstantiated) {

						SensorUIVisualizer visualizer = null;

						// 实例化模型
						visualizer = Instantiate(SensorUIVisualizerPrefab, hit.Pose.position, hit.Pose.rotation);

						visualizer.sensor = sensor;

						if(((DetectedPlane)hit.Trackable).PlaneType == DetectedPlaneType.Vertical) {
							visualizer.transform.Rotate(90.0f, 0, 0, Space.Self);
						};

						// 在碰撞检测点创建锚点
						Anchor anchor = hit.Trackable.CreateAnchor(hit.Pose);

						// 将模型绑定为锚点的子对象
						visualizer.transform.parent = anchor.transform;

						textHasInstantiated = true;

						SnackBar.SetActive(false);
					}
					// else {
					// 	_ShowAndroidToastMessage("已经实例化了");
					// }
				}
			}
		}
	}

	private void _UpdateApplicationLifecycle () {
		if (Input.GetKey(KeyCode.Escape)) {
			Application.Quit();
		}

		if (Session.Status != SessionStatus.Tracking) {
			const int lostTrackingSleepTimeout = 15;
			Screen.sleepTimeout = lostTrackingSleepTimeout;
		}
		else {
			Screen.sleepTimeout = SleepTimeout.NeverSleep;
		}

		if (m_IsQuitting) {
			return;
		}

		if (Session.Status == SessionStatus.ErrorPermissionNotGranted) {
			_ShowAndroidToastMessage("应用程序需要系统相机权限");
			m_IsQuitting = true;
			Invoke("_DoQuit", 0.5f);
		}
		else if (Session.Status.IsError()) {
			_ShowAndroidToastMessage("ARCore遇到了未知问题，请重新启动本程序");
			m_IsQuitting = true;
			Invoke("_DoQuit", 0.5f);
		}
	}

	private void _DoQuit () {
		Application.Quit();
	}

	private void _ShowAndroidToastMessage (string message) {
		AndroidJavaClass UnityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
		AndroidJavaObject unityActivity = UnityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

		if (unityActivity != null) {
			AndroidJavaClass ToastClass = new AndroidJavaClass("android.widget.Toast");
			unityActivity.Call("runOnUiThread", new AndroidJavaRunnable(() => {
				AndroidJavaObject toastObject = ToastClass.CallStatic<AndroidJavaObject>("makeText", unityActivity,
					message, 0);
				toastObject.Call("show");
			}));
		}
	}

	private string ScanQRCode () {
		data = screenTexture.GetPixels32();
		Result result = barcodeReader.Decode(data, screenTexture.width, screenTexture.height);
		if (result != null) {
			// _ShowAndroidToastMessage(result.Text);
			_ShowAndroidToastMessage("二维码扫描成功");
			SnackBarText.text = "请开始寻找环境中的平面并点击屏幕进行数据显示";
			PlaneGenerator.SetActive(true);
			ScanOverlay.SetActive(false);
			return result.Text;
			sensorMac = result.Text;
		}
		else {
			_ShowAndroidToastMessage("没有扫描到二维码");
			return null;
		}
	}

	IEnumerator RequestSensorData (string sensorMac) {

		string url = "https://api.easylinkin.com/api/v1/application/data" + "?mac=" + sensorMac + "&token=123456&cid=226";

		// _ShowAndroidToastMessage(url);

		using (WWW www = new WWW(url)) {
			yield return www;

			// if (www.error != null) {
			// 	_ShowAndroidToastMessage(www.error);
			// } else {
			// 	_ShowAndroidToastMessage(www.text);
			// }

			JsonData jd = JsonMapper.ToObject(www.text);
			string appeui = (string)jd["appeui"];
			string mac = (string)jd["mac"];
			string data = (string)jd["data"];
			string last_update_time = (string)jd["last_update_time"];
			int data_type = (int)jd["data_type"];
			int status = (int)jd["status"];
			Debug.Log("appeui=" + appeui);
			Debug.Log("mac=" + mac);
			Debug.Log("data=" + data);
			Debug.Log("last_update_time=" + last_update_time);
			Debug.Log("data_type=" + data_type);
			Debug.Log("status=" + status);

			if (data.Length == 20) {
				string batteryH = data.Substring(2, 2);
				string signalH = data.Substring(4, 2);
				string tempIntH = data.Substring(10, 2);
				string tempIntB = System.Convert.ToString(System.Convert.ToInt32("0x" + tempIntH, 16), 2);
				if (tempIntB.Length == 8) {} 
				else {
					for (int i = 0, length = 8 - tempIntB.Length; i < length; i++ ) {
						tempIntB = "0" + tempIntB;
					}
				}
				string tempFraH = data.Substring(12, 2);
				string humidityH = data.Substring(14, 2);
				int battery = System.Convert.ToInt32("0x" + batteryH, 16);
				int signal = System.Convert.ToInt32("0x" + signalH, 16);
				signal = -signal;
				int tempPlusMinus = System.Convert.ToInt32(tempIntB.Substring(0, 1), 2);
				int tempInt = System.Convert.ToInt32(tempIntB.Substring(1), 2);
				int tempFra = System.Convert.ToInt32("0x" + tempFraH, 16);
				int humidity = System.Convert.ToInt32("0x" + humidityH, 16);
				double temperature = tempInt + (double)tempFra / 100;
				temperature = tempPlusMinus==1 ? -temperature : temperature;

				// Debug.Log("batteryH=" + batteryH);
				// Debug.Log("signalH=" + signalH);
				// Debug.Log("tempIntH=" + tempIntH);
				// Debug.Log("tempIntB=" + tempIntB);
				// Debug.Log("tempFraH=" + tempFraH);
				// Debug.Log("humidityH=" + humidityH);
				// Debug.Log("battery=" + battery);
				// Debug.Log("signal=" + signal);
				// Debug.Log("tempPlusMinus=" + tempPlusMinus);
				// Debug.Log("tempInt=" + tempInt);
				// Debug.Log("tempFra=" + tempFra);
				// Debug.Log("humidity=" + humidity);
				// Debug.Log("temperature=" + temperature);

				sensor = new Sensor(mac, last_update_time, status, temperature, humidity, signal, battery);
			}
			
		}

	}

}
