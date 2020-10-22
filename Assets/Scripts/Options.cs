using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

public class Options : MonoBehaviour
{
    public ImportExport io;
    public TMP_InputField qualityInput;

    public void SetUseExistingBasis(bool use) {
        io.useExistingBasis = use;
    }

    public void SetPreview(bool use) {
        io.showPreview = use;
    }

    public void SetUseMultithreading(bool use) {
        io.useMultithreading = use;
    }

    public void SetQuality(string quality) {
        int q;
        try {
            q = Int32.Parse(quality);
            if(q > 255)
                q = 255;
            else if (q < 0)
                q = 0;

            qualityInput.text = q.ToString();
            io.quality = q;
        } catch (Exception e) {
            // Logging.Log(quality + " is not a valid value!");
        }
    }
}
