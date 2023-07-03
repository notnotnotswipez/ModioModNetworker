using ModioModNetworker.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModioModNetworker.UI
{
    public class CheckboxSetting : GenericSetting
    {
        public CheckboxSetting(string title, bool startingValue, Action<bool> onChecked = null)
        {
            prefabObject = NetworkerAssets.checkboxSettingPrefab;
            this.title = title;
            value = startingValue;
            this.onChecked = onChecked;
        }

        Button checkBox;
        public bool value;
        public Action<bool> onChecked;


        public override void SpawnPrefab(Transform parent) {
            GameObject spawnedElement = GameObject.Instantiate(prefabObject);
            checkBox = spawnedElement.transform.Find("Button").GetComponent<Button>();
            checkBox.onClick.AddListener(new Action(() => {
                OnCheckMarkClicked();
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

        private void OnCheckMarkClicked() {
            value = !value;
            UpdateDisplay();
            if (onChecked != null) {
                onChecked.Invoke(value);
            }
        }

        private void UpdateDisplay() {
            GameObject unCheckedImage = spawnedObject.transform.Find("UnCheckedImage").gameObject;
            GameObject checkedImage = spawnedObject.transform.Find("CheckedImage").gameObject;

            if (value)
            {
                unCheckedImage.SetActive(false);
                checkedImage.SetActive(true);
            }
            else { 
                unCheckedImage.SetActive(true);
                checkedImage.SetActive(false);
            }
        }
    }
}
