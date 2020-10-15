using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Threading.Tasks;

public class ExportButton : MonoBehaviour, IPointerClickHandler
{
    public ImportExport io;

    public async void OnPointerClick(PointerEventData ev) {
        if (ev.button == PointerEventData.InputButton.Left)
            await io.Export();
    }
}
