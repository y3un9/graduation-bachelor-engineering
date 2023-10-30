using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sensor {

	public Sensor (string mac, string last_update_time, int status, double temperature,
		int humidity, int signal, int battery) {
			
			Mac = mac;

			Last_update_time = last_update_time;

			Status = status;

			Temperature = temperature;

			Humidity = humidity;

			Signal = signal;

			Battery = battery;	
	}

	public string Mac;

	public string Last_update_time;

	public int Status;

	public double Temperature;

	public int Humidity;

	public int Signal;

	public int Battery;

}
