using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.Assertions;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace Scoops.customization
{
    internal class CustomizationMenuUtils
    {
        // thank you lethalconfig for the injectmenu code
        // why does the menu not have a vertical layout group
        internal static void InjectMenu(Transform mainButtonsTransform, GameObject quitButton)
        {
            // Adding customization gui to scene
            CustomizationManager.SpawnCustomizationGUI();

            // Cloning main menu button for opening phone customization
            var clonedButton = UnityEngine.Object.Instantiate(quitButton, mainButtonsTransform);
            clonedButton.GetComponent<Button>().onClick.RemoveAllListeners();
            clonedButton.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
            clonedButton.GetComponent<Button>().onClick.AddListener(CustomizationManager.ActivateCustomizationPanel);

            clonedButton.GetComponentInChildren<TextMeshProUGUI>().text = "> Personalize Phone";

            // Offsets all buttons inside the main buttons.
            var buttonsList = mainButtonsTransform.GetComponentsInChildren<Button>()
                .Select(b => b.gameObject);

            // Gets the smallest distance between two buttons.
            var gameObjects = buttonsList.ToList();
            var positions = gameObjects
                .Where(b => b != clonedButton)
                .Select(b => b.transform as RectTransform)
                .Select(t => t!.anchoredPosition.y);
            var enumerable = positions.ToList();
            var offsets = enumerable
                .Zip(enumerable.Skip(1), (y1, y2) => Mathf.Abs(y2 - y1));
            var offset = offsets.Min();

            foreach (var button in gameObjects.Where(g => g != quitButton))
                button.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, offset);

            clonedButton.GetComponent<RectTransform>().anchoredPosition =
                quitButton.GetComponent<RectTransform>().anchoredPosition + new Vector2(0, offset);
        }
    }
}
