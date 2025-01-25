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
        public void UpdateTextClientRpc(ulong[] phoneIds)
        {
            if (textArea == null)
            {
                textArea = transform.Find("Paper").Find("PaperCanvas").Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
            }

            string newClipboardText = "";

            foreach (ulong phoneId in phoneIds)
            {
                PlayerPhone playerPhone = GetNetworkObject(phoneId).GetComponent<PlayerPhone>();
                newClipboardText += playerPhone.phoneNumber + " - " + playerPhone.player.playerUsername + "\n";
            }

            textArea.text = newClipboardText;
        }
    }
}
