using Scoops.service;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace Scoops.misc
{
    public class Clipboard : NetworkBehaviour
    {
        public TextMeshProUGUI textArea;

        public Renderer clipboardRenderer;

        public void Start()
        {
            // We need an update!
            PhoneNetworkHandler.Instance.UpdateClipboardText();

            clipboardRenderer = transform.Find("Board").GetComponent<Renderer>();
        }

        //tbh I do not like this
        public void Update()
        {
            if (clipboardRenderer.enabled != textArea.enabled)
            {
                textArea.enabled = clipboardRenderer.enabled;
            }
        }

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
