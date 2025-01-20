using MelonLoader;
using ModioModNetworker.Data;
using ModioModNetworker.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModIoModNetworker.Ui
{
    [RegisterTypeInIl2Cpp]
    public class ModInfoDisplay : MonoBehaviour
    {

        public ModInfoDisplay(IntPtr intPtr) : base(intPtr)
        {
        }

        public TMP_Text title;
        private RawImage thumbnailImage;
        public RawImage borderImage;
        public ModInfo modInfo;
        public Button button;
        public NetworkerMenuController controller;
        public GameObject subscriptionButton;

        private bool hasAddedThumbnail = false;


        public void Awake() {
            thumbnailImage = transform.Find("Thumbnail").GetComponent<RawImage>();
            title = transform.Find("Text (TMP)").GetComponent<TMP_Text>();
            borderImage = transform.Find("BaseOverlay").GetComponent<RawImage>();
            button = transform.Find("Button").GetComponent<Button>();
            subscriptionButton = transform.Find("SubscribedIndicator").gameObject;
            button.onClick.AddListener(new Action(() =>
            {
                OnModInfoPressed();
            }));
        }

        public void OnModInfoPressed() {
            controller.TriggerModInfoPopup(true, modInfo);
        }

        public void SetModInfo(ModInfo modInfo) {

            this.modInfo = modInfo;
            title.text = modInfo.modName;
            if (!modInfo.IsSubscribed())
            {
                subscriptionButton.SetActive(false);
            }
            else {
                subscriptionButton.SetActive(true);
            }
            ThumbnailThreader.DownloadThumbnail(modInfo.thumbnailLink, (texture =>
            {
                if (thumbnailImage) {
                    thumbnailImage.texture = texture;
                    hasAddedThumbnail = true;
                }
            }));
        }

        public void DestroyThumbnail() {
            if (hasAddedThumbnail) {
                DestroyImmediate(thumbnailImage.texture);
            }
        }
    }
}
