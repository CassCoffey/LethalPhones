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
            clipboardRenderer = transform.Find("Board").GetComponent<Renderer>();
            textArea = transform.Find("Paper/PaperCanvas/Text (TMP)").GetComponent<TextMeshProUGUI>();

            PhoneNetworkHandler.Instance.phoneListUpdateEvent.AddListener(UpdateText);

            // We need an update!
            PhoneNetworkHandler.Instance.RequestPhoneListUpdates();
        }

        //tbh I do not like this
        public void Update()
        {
            if (clipboardRenderer.enabled != textArea.enabled)
            {
                textArea.enabled = clipboardRenderer.enabled;
            }
        }

        public void UpdateText()
        {
            if (textArea == null)
            {
                textArea = transform.Find("Paper/PaperCanvas/Text (TMP)").GetComponent<TextMeshProUGUI>();
            }

            string newClipboardText = "";

            foreach (PhoneBehavior phone in PhoneNetworkHandler.allPhoneBehaviors)
            {
                if (phone is PlayerPhone)
                {
                    newClipboardText += phone.phoneNumber.ToString("D4") + " - " + phone.GetPhoneName() + "\n";
                }
            }

            textArea.text = newClipboardText;
        }
    }
}
