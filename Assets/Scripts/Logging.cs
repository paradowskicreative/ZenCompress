using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;


public class Logging : MonoBehaviour
{
	public static Action<string> logMsg;
	public ScrollRect scroll;
	private TextMeshProUGUI consoleText;

	public static void Log(string msg)
	{
		Logging.logMsg?.Invoke(msg);
	}

	private void OnEnable()
	{
		if (!consoleText)
			consoleText = GetComponent<TextMeshProUGUI>();
		logMsg += LogToConsole;
	}

	private void OnDisable()
	{
		logMsg -= LogToConsole;
	}

	private void LogToConsole(string msg)
	{
		consoleText.text += msg + "\n";
		StartCoroutine(ScrollAfterUpdate());
	}

	// Scroll after the frame is finished - otherwise the text doesn't have time to update.
	IEnumerator ScrollAfterUpdate()
	{
		yield return new WaitForEndOfFrame();
		scroll.verticalNormalizedPosition = 0;
	}
}

