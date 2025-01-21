using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;

namespace Scoops.misc
{
    public class Clipboard : NetworkBehaviour
    {
        public TextMeshProUGUI textArea;

        [ClientRpc]
        public void UpdateTextClientRpc(string phonetext)
        {
            if (textArea == null)
            {
                textArea = transform.Find("Paper").Find("PaperCanvas").Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            }

            textArea.text = phonetext;
        }
    }
}
