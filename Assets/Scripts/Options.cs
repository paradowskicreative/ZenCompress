using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

public class Options : MonoBehaviour
{
	public ImportExport io;
	public TMP_InputField qualityInput;
	public TMP_InputField thresholdInput;
	public TMP_InputField levelInput;

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

	public void SetQuality(string quality)
	{
		int q;
		try
		{
			q = Int32.Parse(quality);
			if (q > 255)
				q = 255;
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
			t = Mathf.Clamp(t, 1f, 2f);
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
