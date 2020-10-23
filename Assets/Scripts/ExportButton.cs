using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading.Tasks;
using UnityEngine.UI;

public class ExportButton : MonoBehaviour, IPointerClickHandler
{
    public ImportExport io;
    public Button button;

    public async void OnPointerClick(PointerEventData ev) {
        if (ev.button == PointerEventData.InputButton.Left && button.interactable)
            await io.Export();
    }
}
