using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System;
using TMPro;

public class Options : MonoBehaviour
{
	public ImportExport io;
	public TextMeshProUGUI qualityLabel;
	public TMP_InputField qualityInput;
	public TextMeshProUGUI thresholdLabel;
	public TMP_InputField thresholdInput;
	public TMP_InputField levelInput;
	public static Options instance;

	int qualityLimiter = 4;
	float thresholdLowerLimiter = 0.2f;
	float thresholdUpperLimiter = 3f;
	Int32 previousSubFormat = 1;
	string previousUASTCQuality = "4";
	string previousETC1SQuality = "255";
	string previousUASTCThreshold = "0.75";
	string previousETC1SThreshold = "1.05";

	void Awake()
	{
		instance = this;
	}

	public void SetTextureFormat(Int32 format)
	{
		io.format = (ImportExport.Format)(format < 2 ? 1 : 0);
		io.subFormat = (ImportExport.SubFormat)(format % 2 == 0 ? 1 : 0);

		if (io.subFormat == ImportExport.SubFormat.UASTC && (Int32)io.subFormat != previousSubFormat) {
			qualityLabel.text = "Encoding quality [0-4]";
			thresholdLabel.text = "RDO Threshold [0.2-3.0]";
			previousETC1SQuality = qualityInput.text;
			previousETC1SThreshold = thresholdInput.text;
			qualityInput.text = previousUASTCQuality;
			io.quality = Int32.Parse(previousUASTCQuality);
			thresholdInput.text = previousUASTCThreshold;
			io.threshold = float.Parse(previousUASTCThreshold);
			qualityLimiter = 4;
			thresholdLowerLimiter = 0.2f;
			thresholdUpperLimiter = 3f;
			previousSubFormat = (Int32)io.subFormat;
		} else if((Int32)io.subFormat != previousSubFormat) {
			qualityLabel.text = "Encoding quality [0-255]";
			thresholdLabel.text = "RDO Threshold [1.0-2.0]";
			previousUASTCQuality = qualityInput.text;
			previousUASTCThreshold = thresholdInput.text;
			qualityInput.text = previousETC1SQuality;
			io.quality = Int32.Parse(previousETC1SQuality);
			thresholdInput.text = previousETC1SThreshold;
			io.threshold = float.Parse(previousETC1SThreshold);
			qualityLimiter = 255;
			thresholdLowerLimiter = 1f;
			thresholdUpperLimiter = 2f;
			previousSubFormat = (Int32)io.subFormat;
		}
	}

	public void SetUseExistingBasis(bool use)
	{
		io.useExistingBasis = use;
	}

	public void SetPreview(bool use)
	{
		io.showPreview = use;
	}

	public void SetUseMultithreading(bool use)
	{
		io.useMultithreading = use;
	}

	public void SetPreserveAlpha(bool use)
	{
		io.preserveAlpha = use;
	}

	public void SetConvertLightmaps(bool use)
	{
		io.convertLightmaps = use;
	}

	public void SetTextureToggle(int toggle)
	{
		if (toggle < io.conversionToggles.Length)
		{
			io.conversionToggles[toggle] ^= true;

			var group = TextureList.instance.textureList.Where(entry => entry.mapType == (TextureList.MapType)toggle);

			foreach (var item in group)
			{
				item.toggled = io.conversionToggles[toggle];
				item.toggle.isOn = io.conversionToggles[toggle];
			}

		}
	}

	public void SetQuality(string quality)
	{
		int q;
		try
		{
			q = Int32.Parse(quality);
			int max = io.subFormat == ImportExport.SubFormat.UASTC ? 4 : 255;
			if (q > max)
				q = max;
			else if (q < 0)
				q = 0;

			qualityInput.text = q.ToString();
			io.quality = q;
		}
		catch (Exception e)
		{
			Logging.Log(e.ToString());
		}
	}

	public void SetThreshold(string threshold)
	{
		float t;
		try
		{
			t = float.Parse(threshold);
			t = Mathf.Clamp(t, thresholdLowerLimiter, thresholdUpperLimiter);
			thresholdInput.text = t.ToString("0.00");
			io.threshold = t;
		}
		catch (Exception e)
		{
			Logging.Log(e.ToString());
		}
	}

	public void SetCompLevel(string level)
	{
		int l;
		try
		{
			l = Int32.Parse(level);
			if (l > 5)
				l = 5;
			else if (l < 1)
				l = 1;

			levelInput.text = l.ToString();
			io.level = l;
		}
		catch (Exception e)
		{
			Logging.Log(e.ToString());
		}
	}
}
