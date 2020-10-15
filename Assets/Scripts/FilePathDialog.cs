using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SFB;
using System.Linq;
using TMPro;

public class FilePathDialog : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField input;

    [SerializeField]
    private TMP_InputField output;

    public void SetFilePath() {
        var fp = GetFilePath();

        if(fp != null)
            input.text = fp;
    }

    public void SetFolderPath() {
        var fp = GetFolderPath();

        if(fp != null)
            output.text = fp;
    }

    public string GetFilePath() {
        var extensions = new [] {
            new ExtensionFilter("glTF Separate", "gltf"),
            new ExtensionFilter("All Files", "*"),
        };
        string[] path = StandaloneFileBrowser.OpenFilePanel("Select .gltf", "", extensions, false);
        return path.ElementAtOrDefault(0);
    }

    public string GetFolderPath() {
        var extensions = new [] {
            new ExtensionFilter("glTF Binary", "glb"),
            new ExtensionFilter("All Files", "*"),
        };
        string path = StandaloneFileBrowser.SaveFilePanel("Save File As", "", "", extensions);
        return path;
    }
}
