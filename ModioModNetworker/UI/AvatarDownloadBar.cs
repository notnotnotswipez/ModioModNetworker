using System.Collections.Generic;
using LabFusion.Extensions;
using LabFusion.Representation;
using ModioModNetworker.Utilities;
using Il2CppSLZ.Rig;
using Il2CppTMPro;
using UnityEngine;

namespace ModioModNetworker.UI
{
    public class AvatarDownloadBar
    {
        private PlayerRep rep;
        private GameObject bar;
        private GameObject fill;
        private Animator animator;
        private RigManager manager;
        
        private TMP_Text modNameText;
        private TMP_Text percentageText;
        
        private float zeroPosition = 86f;
        private float completePosition = 0f;
        
        private bool previouslyStarted = false;

        public static Dictionary<PlayerId, AvatarDownloadBar> bars = new Dictionary<PlayerId, AvatarDownloadBar>();

        public AvatarDownloadBar(PlayerRep rep)
        {
            GameObject go = GameObject.Instantiate(NetworkerAssets.avatarDownloadBarPrefab);
            GameObject.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontUnloadUnusedAsset;
            bar = go;
            go.SetActive(false);
            if (bars.TryGetValue(rep.PlayerId, out var bar1))
            {
                GameObject existingBar = bar1.bar;
                GameObject.Destroy(existingBar);
                bars.Remove(rep.PlayerId);
            }
            bars.Add(rep.PlayerId, this);

            animator = go.GetComponent<Animator>();
            this.rep = rep;
            manager = rep.RigReferences.RigManager;
            modNameText = go.transform.Find("Bar").Find("ModName").GetComponent<TMP_Text>();
            percentageText = go.transform.Find("Bar").Find("Percentage").GetComponent<TMP_Text>();
            fill = go.transform.Find("Bar").Find("Mask").Find("Fill").gameObject;
            fill.transform.localPosition = new Vector3(zeroPosition, 0f, 0f);
            
        }
        
        private float GetBarOffset(RigManager rm)
        {
            float offset = 0.3f;

            if (rm._avatar)
                offset *= rm._avatar.height;

            return offset;
        }

        public void Update()
        {
            if (manager)
            {
                var head = manager.physicsRig.m_head;
                bar.transform.position = head.position + Vector3.up * GetBarOffset(manager);
                bar.transform.LookAtPlayer();
            }
        }
        
        public void SetModName(string name)
        {
            modNameText.text = name;
        }
        
        public void Finish()
        {
            animator.SetTrigger("completed");
            previouslyStarted = false;
        }
        
        public void SetPercentage(float percentage)
        {
            if (!previouslyStarted)
            {
                Show();
            }
            string safeDisplay = percentage.ToString("0.0");
            percentageText.text = $"{safeDisplay}%";
            float position = zeroPosition - (percentage / 100f) * (zeroPosition - completePosition);
            RectTransform rt = fill.GetComponent<RectTransform>();
            rt.localPosition = new Vector3(position, 0f, 0f);
        }

        public void Show()
        {
            bar.SetActive(true);
            animator.SetTrigger("reset");
            previouslyStarted = true;
        }
    }
}