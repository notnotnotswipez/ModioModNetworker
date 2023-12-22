using ModioModNetworker.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModioModNetworker.UI
{
    public class NumericalSetting : GenericSetting
    {
        public NumericalSetting(string title, int startingValue, int minValue, int maxValue, int increment, Action<int> onModified = null)
        {
            prefabObject = NetworkerAssets.numericalSettingPrefab;
            this.title = title;
            value = startingValue;
            this.onModified = onModified;
            this.minValue = minValue;
            this.maxValue = maxValue;
            this.increment = increment;
        }

        Button increaseButton;
        Button decreaseButton;
        public int value;
        public Action<int> onModified;
        public int minValue;
        public int maxValue;
        public int increment;

        public override void SpawnPrefab(Transform parent) {
            GameObject spawnedElement = GameObject.Instantiate(prefabObject);
            increaseButton = spawnedElement.transform.Find("IncreaseArrow").Find("Button").GetComponent<Button>();
            decreaseButton = spawnedElement.transform.Find("DecreaseArrow").Find("Button").GetComponent<Button>();

            increaseButton.onClick.AddListener(new Action(() => {
                ModifyValue(true);
            }));

            decreaseButton.onClick.AddListener(new Action(() => {
                ModifyValue(false);
            }));

            TMP_Text titleText = spawnedElement.transform.Find("Title").GetComponent<TMP_Text>();
            titleText.text = title;

            spawnedElement.transform.parent = parent;
            spawnedElement.transform.localPosition = Vector3.forward;
            spawnedElement.transform.localRotation = Quaternion.identity;
            spawnedElement.transform.localScale = Vector3.one;

            spawnedObject = spawnedElement;
            UpdateDisplay();
        }

        private void ModifyValue(bool increase) {
            if (increase)
            {
                value+=increment;
            }
            else {
                value-=increment;
            }

            if (value < minValue) {
                value = minValue;
            }

            if (value > maxValue) {
                value = maxValue;
            }

            UpdateDisplay();
            if (onModified != null) {
                onModified.Invoke(value);
            }
        }

        private void UpdateDisplay() {
            TMP_Text numberText = spawnedObject.transform.Find("NumericalDisplay").GetComponent<TMP_Text>();

            numberText.text = value.ToString();
        }
    }
}
