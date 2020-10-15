using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Options : MonoBehaviour
{
    public ImportExport io;

    public void SetUseExistingBasis(bool use) {
        io.useExistingBasis = use;
    }
}
