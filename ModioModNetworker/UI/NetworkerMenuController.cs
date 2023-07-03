using BoneLib.BoneMenu;
using LabFusion.Network;
using MelonLoader;
using ModioModNetworker;
using ModioModNetworker.Data;
using ModioModNetworker.Repo;
using ModioModNetworker.UI;
using ModioModNetworker.Utilities;
using Steamworks.Ugc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.WebPages;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;
using static UnityEngine.UI.Image;

namespace ModIoModNetworker.Ui
{
    [RegisterTypeInIl2Cpp]
    public class NetworkerMenuController : MonoBehaviour
    {
        public enum Panels { 
            MODIO,
            FILES,
            SETTINGS,
            MULTIPLAYER,
            NONE
        }

        public enum InstalledSort { 
            INSTALLED,
            SUBSCRIBED,
            BLACKLIST
        }

        public NetworkerMenuController(IntPtr intPtr) : base(intPtr) 
        { 
        }

        Panels selectedPanel = Panels.NONE;
        InstalledSort chosenSort = InstalledSort.INSTALLED;

        GameObject modIoTab;
        GameObject filesTab;
        GameObject settingsTab;
        GameObject multiplayerTab;

        GameObject modProgressDisplay;
        GameObject keyboardPopup;

        Button upArrowButton;
        Button downArrowButton;

        TMP_Text typeBarText;
        GameObject typeBarTextObject;
        GameObject typeBarEmptyTextObject;
        GameObject typeBarObject;

        Transform selector;
        Transform desired;
        float speed = 10f;

        Button modIoTabButton;
        Button filesTabButton;
        Button settingsTabButton;
        Button multiplayerTabButton;

        GameObject modInfoPopup;

        int pageNumber = 0;
        int maxPages = 0;
        int maxDisplayPerPage = 8;
        int trendingOffset = 0;
        bool searching = false;

        Animator rootAnimator;
        public string lastDownloadedTitle = "nothing";

        public static List<ModInfo> totalInstalled = new List<ModInfo>();
        public static List<ModInfo> modIoRetrieved = new List<ModInfo>();
        public static List<ModInfo> host = new List<ModInfo>();
        private static List<GenericSetting> settings = new List<GenericSetting>();

        private static List<StalledAction> stalledActions = new List<StalledAction>();

        ModInfo viewedInfo;

        public static NetworkerMenuController instance;

        
        void Awake()
        {
            instance = this;
            selector = transform.Find("Selector");

            modIoTab = transform.Find("ModIoTab").gameObject;
            filesTab = transform.Find("FilesTab").gameObject;
            settingsTab = transform.Find("SettingsTab").gameObject;
            multiplayerTab = transform.Find("MultiplayerTab").gameObject;

            Button backButton = transform.Find("BackButton").GetComponent<Button>();
            backButton.onClick.AddListener(new Action(() => {
                MenuManager.SelectCategory(MenuManager.RootCategory);
            }));

            Button installAllConfirm = multiplayerTab.transform.Find("Confirmer").Find("Confirm").Find("Button").GetComponent<Button>();
            installAllConfirm.onClick.AddListener(new Action(() => {
                foreach (var modInfo in host)
                {
                    ModFileManager.AddToQueue(new DownloadQueueElement()
                    {
                        associatedPlayer = null,
                        info = modInfo
                    });
                }
            }));

            Button refreshSubscribedButton = modIoTab.transform.Find("RefreshSubscribedButton").Find("Button").GetComponent<Button>();
            refreshSubscribedButton.onClick.AddListener(new Action(() => {
                MainClass.PopulateSubscriptions();
            }));

            Button searchButton = modIoTab.transform.Find("SearchIcon").GetComponent<Button>();
            searchButton.onClick.AddListener(new Action(() => {
                PopupKeyboard();
            }));

            upArrowButton = transform.Find("UpArrow").Find("Button").GetComponent<Button>();
            downArrowButton = transform.Find("DownArrow").Find("Button").GetComponent<Button>();
            upArrowButton.onClick.AddListener(new Action(() => {
                OnArrowPress(true);
            }));
            downArrowButton.onClick.AddListener(new Action(() => {
                OnArrowPress(false);
            }));

            modIoTabButton = transform.Find("SelectableTabs").Find("ModIoTab").GetComponentInChildren<Button>();
            filesTabButton = transform.Find("SelectableTabs").Find("FileManagementTab").GetComponentInChildren<Button>();
            settingsTabButton = transform.Find("SelectableTabs").Find("SettingsTab").GetComponentInChildren<Button>();
            multiplayerTabButton = transform.Find("SelectableTabs").Find("MultiplayerTab").GetComponentInChildren<Button>();

            modIoTab.transform.Find("BackToTrending").Find("BackArrow").Find("Button").gameObject.GetComponent<Button>().onClick.AddListener(new Action(() => { ReturnToTrending(); }));

            modIoTabButton.onClick.AddListener(new Action(() =>
            {
                ChangePanel(Panels.MODIO);
            }));
            filesTabButton.onClick.AddListener(new Action(() =>
            {
                ChangePanel(Panels.FILES);
            }));
            settingsTabButton.onClick.AddListener(new Action(() =>
            {
                ChangePanel(Panels.SETTINGS);
            }));
            multiplayerTabButton.onClick.AddListener(new Action(() =>
            {
                ChangePanel(Panels.MULTIPLAYER);
            }));

            modInfoPopup = transform.parent.Find("ModInfoOverlay").Find("ModInfoPopup").gameObject;
            modProgressDisplay = transform.parent.Find("ModInstallingDisplay").gameObject;
            keyboardPopup = transform.parent.Find("KeyboardOverlay").gameObject;

            typeBarObject = keyboardPopup.transform.Find("TypeBar").gameObject;
            typeBarTextObject = typeBarObject.transform.Find("TypedOutText").gameObject;
            typeBarEmptyTextObject = typeBarObject.transform.Find("EmptyTextDisplay").gameObject;
            typeBarText = typeBarTextObject.GetComponent<TMP_Text>();

            rootAnimator = GetComponentInParent<Animator>();

            Button unselectedSubscribe = modInfoPopup.transform.Find("SubscribeUnselected").gameObject.GetComponentInChildren<Button>();
            Button selectedSubscribe = modInfoPopup.transform.Find("SubscribeSelected").gameObject.GetComponentInChildren<Button>();

            Button unselectedBlacklistPart = modInfoPopup.transform.Find("BlacklistParted").gameObject.GetComponentInChildren<Button>();
            Button selectedBlacklistPart = modInfoPopup.transform.Find("BlacklistPartedSelected").gameObject.GetComponentInChildren<Button>();

            Button unselectedInstallPart = modInfoPopup.transform.Find("UninstallParted").gameObject.GetComponentInChildren<Button>();
            Button selectedInstallPart = modInfoPopup.transform.Find("UninstallPartedSelected").gameObject.GetComponentInChildren<Button>();

            Button unselectedBlacklistWhole = modInfoPopup.transform.Find("BlacklistFull").gameObject.GetComponentInChildren<Button>();
            Button selectedBlacklistWhole = modInfoPopup.transform.Find("BlacklistSelectedFull").gameObject.GetComponentInChildren<Button>();

            Button exitCatcherLeft = modInfoPopup.transform.Find("ExitCatch").gameObject.GetComponentInChildren<Button>();
            Button exitCatcherRight = modInfoPopup.transform.Find("ExitCatch (1)").gameObject.GetComponentInChildren<Button>();

            Button installedTab = filesTab.transform.Find("InstalledText").GetComponent<Button>();
            Button subscribedTab = filesTab.transform.Find("SubscribedText").GetComponent<Button>();
            Button blacklistTab = filesTab.transform.Find("BlacklistText").GetComponent<Button>();

            RegisterWholeKeyboard();

            installedTab.onClick.AddListener(new Action(() => {
                SetFilterMode(InstalledSort.INSTALLED);
            }));
            subscribedTab.onClick.AddListener(new Action(() => {
                SetFilterMode(InstalledSort.SUBSCRIBED);
            }));
            blacklistTab.onClick.AddListener(new Action(() => {
                SetFilterMode(InstalledSort.BLACKLIST);
            }));

            exitCatcherLeft.onClick.AddListener(new Action(() =>
            {
                TriggerModInfoPopup(false, null);
            }));
            exitCatcherRight.onClick.AddListener(new Action(() =>
            {
                TriggerModInfoPopup(false, null);
            }));

            unselectedSubscribe.onClick.AddListener(new Action(() =>
            {
                OnSubscribeButtonPressed(false);
            }));
            selectedSubscribe.onClick.AddListener(new Action(() =>
            {
                OnSubscribeButtonPressed(true);
            }));

            unselectedBlacklistPart.onClick.AddListener(new Action(() =>
            {
                OnBlacklistButtonPressed(false);
            }));
            selectedBlacklistPart.onClick.AddListener(new Action(() =>
            {
                OnBlacklistButtonPressed(true);
            }));

            unselectedInstallPart.onClick.AddListener(new Action(() =>
            {
                OnInstallButtonPressed(false);
            }));
            selectedInstallPart.onClick.AddListener(new Action(() =>
            {
                OnInstallButtonPressed(true);
            }));

            unselectedBlacklistWhole.onClick.AddListener(new Action(() =>
            {
                OnBlacklistButtonPressed(false);
            }));
            selectedBlacklistWhole.onClick.AddListener(new Action(() =>
            {
                OnBlacklistButtonPressed(true);
            }));

            // Default page is "Files"
            ChangePanel(Panels.FILES);
        }

        public static void SetHostSubscribedMods(List<ModInfo> modInfos) {
            host.Clear();
            host.AddRange(modInfos);
            MainClass.confirmedHostHasIt = true;
        }

        public static void AddCheckboxSetting(string title, bool startingValue, Action<bool> onModified) {
            settings.Add(new CheckboxSetting(title, startingValue, onModified));
        }

        public static void AddNumericalSetting(string title, int startingValue, int minValue, int maxValue, int increment, Action<int> onModified)
        {
            settings.Add(new NumericalSetting(title, startingValue, minValue, maxValue, increment, onModified));
        }

        public void OnSubscribeButtonPressed(bool selected) {
            if (selected)
            {
                ModFileManager.UnSubscribe(viewedInfo.numericalId);
                if (viewedInfo.IsInstalled())
                {
                    ModFileManager.UnInstall(viewedInfo.numericalId);
                }
            }
            else {
                if (!MainClass.subscribedModIoNumericalIds.Contains(viewedInfo.numericalId)) {
                    MainClass.subscribedModIoNumericalIds.Add(viewedInfo.numericalId);
                }
                MelonLogger.Msg("Subscribing to mod... NOT SUBSCRIBED YET!");
                ModFileManager.Subscribe(viewedInfo.numericalId);
                if (viewedInfo.directDownloadLink != null)
                {
                    MainClass.ReceiveSubModInfo(viewedInfo);
                }
                else
                {
                    MainClass.PopulateSubscriptions();
                }
            }
        }

        private void Search(string query) {
            searching = true;
            modIoRetrieved.Clear();
            PopulateModIoTab(0);
            ResetPageNumber();
            trendingOffset = 0;
            UpdateArrowDisplays();
            ModFileManager.QueueTrending(0, query);
            // Close keyboard
            rootAnimator.SetTrigger("keyboardpopup");
            SetMainCanvasColliderState(true);
            modIoTab.transform.Find("BackToTrending").gameObject.SetActive(true);
            modIoTab.transform.Find("BonelabIcon").gameObject.SetActive(false);
            modIoTab.transform.Find("SectionText").GetComponent<TMP_Text>().text = $"\"{query}\"";
        }

        private void ReturnToTrending() {
            searching = false;
            modIoRetrieved.Clear();
            PopulateModIoTab(0);
            ResetPageNumber();
            UpdateArrowDisplays();
            ModFileManager.QueueTrending(0, "");
            modIoTab.transform.Find("BackToTrending").gameObject.SetActive(false);
            modIoTab.transform.Find("BonelabIcon").gameObject.SetActive(true);
            modIoTab.transform.Find("SectionText").GetComponent<TMP_Text>().text = "TRENDING";
        }

        public void OnBlacklistButtonPressed(bool selected)
        {
            if (selected)
            {
                if (viewedInfo.modId != null)
                {
                    MainClass.blacklistedModIoIds.Remove(viewedInfo.modId);
                    MainClass.RemoveLineFromBlacklist(viewedInfo.modId);
                }

                MainClass.blacklistedModIoIds.Remove(viewedInfo.numericalId);
                MainClass.RemoveLineFromBlacklist(viewedInfo.numericalId);

                
            }
            else {
                if (viewedInfo.modId != null)
                {
                    MainClass.blacklistedModIoIds.Add(viewedInfo.modId);
                    MainClass.WriteLineToBlacklist(viewedInfo.modId);
                }
                else {
                    MainClass.blacklistedModIoIds.Add(viewedInfo.numericalId);
                    MainClass.WriteLineToBlacklist(viewedInfo.numericalId);
                }

                if (viewedInfo.IsSubscribed())
                {
                    ModFileManager.UnSubscribe(viewedInfo.numericalId);
                    if (viewedInfo.IsInstalled())
                    {
                        ModFileManager.UnInstall(viewedInfo.numericalId);
                    }
                }
            }
            UpdateModPopupButtons();
        }

        public void OnInstallButtonPressed(bool selected)
        {
            if (selected)
            {
                MainClass.subscribedModIoNumericalIds.Remove(viewedInfo.numericalId);
                ModFileManager.UnInstall(viewedInfo.numericalId);
                if (viewedInfo.IsSubscribed()) {
                    ModFileManager.UnSubscribe(viewedInfo.numericalId);
                }
            }
            else
            {
                if (viewedInfo.directDownloadLink != null)
                {
                    ModFileManager.AddToQueue(new DownloadQueueElement()
                    {
                        info = viewedInfo,
                        associatedPlayer = null,
                        notify = true
                    });
                }
                else {
                    // This means its one of those "invalid" ones that only have the numerical id but not any of the
                    // other data. So we have to request the data ourselves.
                    ModInfo.RequestModInfoNumerical(viewedInfo.numericalId, "install_native");
                }
            }
        }

        void ResetPageNumber() {
            pageNumber = 0;
            maxDisplayPerPage = 8;
            maxPages = 0;
        }

        public void TriggerModInfoPopup(bool show, ModInfo modInfo) {
            MelonLogger.Msg("Triggering mod info popup!");
            rootAnimator = GetComponentInParent<Animator>();
            rootAnimator.SetTrigger("triggerpopup");
            MelonLogger.Msg("Triggered mod info popup!");
            if (show)
            {
                SetMainCanvasColliderState(false);
                TMP_Text title = modInfoPopup.transform.Find("ModTitle").GetComponent<TMP_Text>();
                TMP_Text description = modInfoPopup.transform.Find("Description").GetComponent<TMP_Text>();
                TMP_Text fileSizeDisplay = modInfoPopup.transform.Find("FileSizeDisplay").GetComponent<TMP_Text>();
                RawImage thumbnail = modInfoPopup.transform.Find("Thumbnail").GetComponent<RawImage>();
                title.text = modInfo.modName;
                description.text = modInfo.modSummary;

                float kb = modInfo.fileSizeKB;
                float mb = kb / 1000000;
                float gb = mb / 1000;

                string display = "KB";
                float value = kb;
                if (mb > 1)
                {
                    value = mb;
                    display = "MB";
                }
                if (gb > 1)
                {
                    value = gb;
                    display = "GB";
                }

                // Round to 2 decimal places
                value = Mathf.Round(value * 100f) / 100f;
                fileSizeDisplay.text = $"({value} {display})";

                ThumbnailThreader.DownloadThumbnail(modInfo.thumbnailLink, (texture =>
                {
                    if (thumbnail)
                    {
                        thumbnail.texture = texture;
                    }
                }));
                viewedInfo = modInfo;
                UpdateModPopupButtons();
            }
            else {
                SetMainCanvasColliderState(true);
            }
        }

        private void SetMainCanvasColliderState(bool enabled) {
            foreach (BoxCollider boxCollider in GetComponentsInChildren<BoxCollider>())
            {
                boxCollider.enabled = enabled;
            }
        }

        public void UpdateModPopupButtons() {
            if (viewedInfo == null) {
                return;
            }
            ModInfo modInfo = viewedInfo;
            GameObject unselectedSubscribe = modInfoPopup.transform.Find("SubscribeUnselected").gameObject;
            GameObject selectedSubscribe = modInfoPopup.transform.Find("SubscribeSelected").gameObject;

            GameObject unselectedBlacklistPart = modInfoPopup.transform.Find("BlacklistParted").gameObject;
            GameObject selectedBlacklistPart = modInfoPopup.transform.Find("BlacklistPartedSelected").gameObject;

            GameObject unselectedInstallPart = modInfoPopup.transform.Find("UninstallParted").gameObject;
            GameObject selectedInstallPart = modInfoPopup.transform.Find("UninstallPartedSelected").gameObject;

            GameObject unselectedBlacklistWhole = modInfoPopup.transform.Find("BlacklistFull").gameObject;
            GameObject selectedBlacklistWhole = modInfoPopup.transform.Find("BlacklistSelectedFull").gameObject;

            unselectedSubscribe.SetActive(false);
            selectedSubscribe.SetActive(false);
            unselectedBlacklistPart.SetActive(false);
            selectedBlacklistPart.SetActive(false);
            unselectedInstallPart.SetActive(false);
            selectedInstallPart.SetActive(false);
            unselectedBlacklistWhole.SetActive(false);
            selectedBlacklistWhole.SetActive(false);
            

            bool installed = modInfo.IsInstalled();
            bool subscribed = modInfo.IsSubscribed();

            if (subscribed)
            {
                unselectedSubscribe.SetActive(false);
                selectedSubscribe.SetActive(true);
            }
            else {
                unselectedSubscribe.SetActive(true);
                selectedSubscribe.SetActive(false);
            }
           

            bool shouldBeParted = false;

            if (installed)
            {
                shouldBeParted = true;
                selectedInstallPart.SetActive(true);
                unselectedInstallPart.SetActive(false);
            }

            if (shouldBeParted)
            {
                selectedBlacklistWhole.SetActive(false);
                unselectedBlacklistWhole.SetActive(false);

                if (modInfo.IsBlacklisted())
                {
                    selectedBlacklistPart.SetActive(true);
                    unselectedBlacklistPart.SetActive(false);
                }
                else
                {
                    selectedBlacklistPart.SetActive(false);
                    unselectedBlacklistPart.SetActive(true);
                }
            }
            else
            {
                if (modInfo.IsBlacklisted())
                {
                    selectedBlacklistWhole.SetActive(true);
                    unselectedBlacklistWhole.SetActive(false);
                }
                else
                {
                    selectedBlacklistWhole.SetActive(false);
                    unselectedBlacklistWhole.SetActive(true);
                }
            }
        }

        public void SetFilterMode(InstalledSort installedSort) {
            chosenSort = installedSort;
            ResetPageNumber();
            switch (installedSort)
            {
                case InstalledSort.INSTALLED:
                    filesTab.transform.Find("GridLayout").gameObject.SetActive(true);
                    filesTab.transform.Find("ListLayout").gameObject.SetActive(false);
                    maxPages = (int) Math.Ceiling((double) totalInstalled.Count / (double) maxDisplayPerPage);
                    UpdateArrowDisplays();
                    PopulateFiles(pageNumber);
                    break;
                case InstalledSort.SUBSCRIBED:
                    filesTab.transform.Find("GridLayout").gameObject.SetActive(true);
                    filesTab.transform.Find("ListLayout").gameObject.SetActive(false);
                    List<ModInfo> subscribedInfos = new List<ModInfo>();
                    foreach (ModInfo info in totalInstalled)
                    {
                        if (info.IsSubscribed())
                        {
                            subscribedInfos.Add(info);
                        }
                    }
                    maxPages = (int) Math.Ceiling((double) subscribedInfos.Count / (double) maxDisplayPerPage);
                    UpdateArrowDisplays();
                    PopulateFiles(pageNumber);
                    break;
                case InstalledSort.BLACKLIST:
                    filesTab.transform.Find("GridLayout").gameObject.SetActive(false);
                    filesTab.transform.Find("ListLayout").gameObject.SetActive(true);
                    maxPages = (int) Math.Ceiling((double) MainClass.blacklistedModIoIds.Count / (double) 4);
                    UpdateArrowDisplays();
                    PopulateBlacklist(pageNumber);
                    break;
            }
        }

        public void PopulateBlacklist(int page) {
            GameObject listView = filesTab.transform.Find("ListLayout").gameObject;

            int childCount = listView.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = listView.transform.GetChild(i);
                Destroy(child.gameObject);
            }

            int starting = 4 * page;
            for (int i = 0; i < 4; i++)
            {
                if (MainClass.blacklistedModIoIds.Count > starting + i)
                {
                    string original = MainClass.blacklistedModIoIds[starting + i];
                    string modDisplay = original;
                    bool isNumeric = int.TryParse(modDisplay, out int n);
                    if (isNumeric) {
                        modDisplay = RepoManager.GetRepoModInfoFromModId(modDisplay).modName;
                    }
                    GameObject blacklistDisplay = Instantiate(NetworkerAssets.blacklistDisplayPrefab);
                    TMP_Text displayName = blacklistDisplay.transform.Find("BlacklistedMod").gameObject.GetComponent<TMP_Text>();
                    displayName.text = modDisplay;
                    Button xButton = blacklistDisplay.transform.Find("XButton").Find("Button").gameObject.GetComponent<Button>();

                    xButton.onClick.AddListener(new Action(() => {
                        MainClass.blacklistedModIoIds.Remove(original);
                        maxPages = (int) Math.Ceiling((double) MainClass.blacklistedModIoIds.Count / (double) 4);
                        PopulateBlacklist(pageNumber);
                        MainClass.RemoveLineFromBlacklist(original);
                    }));

                    blacklistDisplay.transform.parent = listView.transform;
                    blacklistDisplay.transform.localPosition = Vector3.forward;
                    blacklistDisplay.transform.localRotation = Quaternion.identity;
                    blacklistDisplay.transform.localScale = Vector3.one;
                }
            }
        }

        public void OnNewTrendingRecieved()
        {
            if (selectedPanel == Panels.MODIO) {
                if (maxPages >= 1)
                {
                    PopulateModIoTab(maxPages - 1);
                }
                else {
                    PopulateModIoTab(0);
                }
                maxPages = (int) Math.Ceiling((double) modIoRetrieved.Count / (double) maxDisplayPerPage);
                UpdateArrowDisplays();
                SetMainCanvasColliderState(true);
            }
        }

        void PopulateModIoTab(int page) {
            GameObject gridView = modIoTab.transform.Find("GridLayout").gameObject;

            int childCount = gridView.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridView.transform.GetChild(i);
                ModInfoDisplay modInfoDisplay = child.GetComponent<ModInfoDisplay>();

                // Manual texture removing nonsense
                DestroyImmediate(modInfoDisplay.thumbnailImage.texture);
                Destroy(child.gameObject);
            }

            int starting = maxDisplayPerPage * page;
            for (int i = 0; i < maxDisplayPerPage; i++)
            {
                if (modIoRetrieved.Count > starting + i)
                {
                    ModInfo modInfo = modIoRetrieved[starting + i];
                    GameObject modInfoPanel = Instantiate(NetworkerAssets.modInfoDisplay);
                    ModInfoDisplay modInfoDisplay = modInfoPanel.AddComponent<ModInfoDisplay>();
                    modInfoDisplay.SetModInfo(modInfo);
                    modInfoDisplay.controller = this;
                    modInfoPanel.transform.parent = gridView.transform;
                    modInfoPanel.transform.localPosition = Vector3.forward;
                    modInfoPanel.transform.localRotation = Quaternion.identity;
                    modInfoPanel.transform.localScale = Vector3.one;
                }
            }
        }

        void PopulateFiles(int page) {
            GameObject gridView = filesTab.transform.Find("GridLayout").gameObject;

            int childCount = gridView.transform.childCount;
            for (int i = 0; i < childCount; i++) {
                Transform child = gridView.transform.GetChild(i);
                ModInfoDisplay modInfoDisplay = child.GetComponent<ModInfoDisplay>();

                // Manual texture removing nonsense
                DestroyImmediate(modInfoDisplay.thumbnailImage.texture);
                Destroy(child.gameObject);
            }

            int shown = 0;
            int loop = 0;
            int starting = page * maxDisplayPerPage;

            foreach (ModInfo modInfo in totalInstalled) {
                loop++;
                if (loop < starting) {
                    continue;
                }
                if (chosenSort == InstalledSort.SUBSCRIBED)
                {
                    if (!modInfo.IsSubscribed())
                    {

                        continue;
                    }
                }
                GameObject modInfoPanel = Instantiate(NetworkerAssets.modInfoDisplay);
                ModInfoDisplay modInfoDisplay = modInfoPanel.AddComponent<ModInfoDisplay>();
                modInfoDisplay.SetModInfo(modInfo);
                modInfoDisplay.controller = this;
                modInfoPanel.transform.parent = gridView.transform;
                modInfoPanel.transform.localPosition = Vector3.forward;
                modInfoPanel.transform.localRotation = Quaternion.identity;
                modInfoPanel.transform.localScale = Vector3.one;
                shown++;
                if (shown >= maxDisplayPerPage) {
                    break;
                }
            }
        }

        void PopulateHostMods(int page)
        {
            GameObject gridView = multiplayerTab.transform.Find("GridLayout").gameObject;

            int childCount = gridView.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = gridView.transform.GetChild(i);
                ModInfoDisplay modInfoDisplay = child.GetComponent<ModInfoDisplay>();

                // Manual texture removing nonsense
                DestroyImmediate(modInfoDisplay.thumbnailImage.texture);
                Destroy(child.gameObject);
            }

            int shown = 0;
            int loop = 0;
            int starting = page * maxDisplayPerPage;

            foreach (ModInfo modInfo in host)
            {
                loop++;
                if (loop < starting)
                {
                    continue;
                }
            
                GameObject modInfoPanel = Instantiate(NetworkerAssets.modInfoDisplay);
                ModInfoDisplay modInfoDisplay = modInfoPanel.AddComponent<ModInfoDisplay>();
                modInfoDisplay.SetModInfo(modInfo);
                modInfoDisplay.controller = this;
                modInfoPanel.transform.parent = gridView.transform;
                modInfoPanel.transform.localPosition = Vector3.forward;
                modInfoPanel.transform.localRotation = Quaternion.identity;
                modInfoPanel.transform.localScale = Vector3.one;
                shown++;
                if (shown >= maxDisplayPerPage)
                {
                    break;
                }
            }
        }

        void PopulateSettings(int page)
        {
            GameObject settingsHolder = settingsTab.transform.Find("SettingsHolder").gameObject;

            int childCount = settingsHolder.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = settingsHolder.transform.GetChild(i);
                Destroy(child.gameObject);
            }

            int starting = 3 * page;
            for (int i = 0; i < 3; i++)
            {
                if (settings.Count > starting + i)
                {
                    GenericSetting genericSetting = settings[starting + i];
                    genericSetting.SpawnPrefab(settingsHolder.transform);
                }
            }
        }

        void ChangePanel(Panels panel) {
            ResetPageNumber();
            selectedPanel = panel;
            switch (panel) {
                case Panels.FILES:
                    SetSelectorDesired(filesTabButton.transform.parent.Find("SelectorDesiredPos"));
                    SetFilterMode(chosenSort);
                    break;
                case Panels.MODIO:
                    maxPages = (int) Math.Ceiling((double) modIoRetrieved.Count / (double) maxDisplayPerPage);
                    UpdateArrowDisplays();
                    SetSelectorDesired(modIoTabButton.transform.parent.Find("SelectorDesiredPos"));
                    PopulateModIoTab(pageNumber);
                    break;
                case Panels.SETTINGS:
                    maxPages = (int) Math.Ceiling((double) settings.Count / (double) 3);
                    UpdateArrowDisplays();
                    SetSelectorDesired(settingsTabButton.transform.parent.Find("SelectorDesiredPos"));
                    PopulateSettings(pageNumber);
                    break;
                case Panels.MULTIPLAYER:
                    maxPages = (int) Math.Ceiling((double) host.Count / (double) maxDisplayPerPage);
                    UpdateArrowDisplays();
                    SetSelectorDesired(multiplayerTabButton.transform.parent.Find("SelectorDesiredPos"));
                    PopulateHostMods(pageNumber);
                    TMP_Text text = multiplayerTab.transform.Find("InstallAllHostModsButton").Find("Text (TMP)").GetComponent<TMP_Text>();

                    float totalSize = 0;
                    int missingMods = 0;
                    foreach (var modInfo in host)
                    {
                        if (!modInfo.IsInstalled())
                        {
                            missingMods++;
                            totalSize += modInfo.fileSizeKB;
                        }
                    }
                    float kb = totalSize;
                    float mb = kb / 1000000;
                    float gb = mb / 1000;

                    string display = "KB";
                    float value = kb;
                    if (mb > 1)
                    {
                        value = mb;
                        display = "MB";
                    }
                    if (gb > 1)
                    {
                        value = gb;
                        display = "GB";
                    }

                    // Round to 2 decimal places
                    value = Mathf.Round(value * 100f) / 100f;
                    text.text = $"Install Host Mods ({missingMods}) ({value} {display})";

                    break;
            }
        }

        void UpdateArrowDisplays() {
            GameObject upArrow = upArrowButton.transform.parent.gameObject;
            GameObject downArrow = downArrowButton.transform.parent.gameObject;
            upArrow.SetActive(pageNumber > 0);
            downArrow.SetActive(pageNumber < maxPages - 1);

            // Set down arrow to always active on the mod.io tab.
            if (selectedPanel == Panels.MODIO) {
                // But only if theres a large amount of mods. It means that there probably IS more to request. (Trending but not searches)
                if (modIoRetrieved.Count > 80) {
                    downArrow.SetActive(true);
                }
            }
        }

        void OnArrowPress(bool up) {
            if (!up)
            {
                pageNumber++;
            }
            else {
                pageNumber--;
            }

            if (pageNumber < 0) {
                pageNumber = 0;
            }

            if (selectedPanel == Panels.MODIO) {
                if (pageNumber == maxPages-1 && modIoRetrieved.Count > 80) {
                    trendingOffset++;
                    if (searching)
                    {
                        // Continue with more search query results
                        ModFileManager.QueueTrending(trendingOffset * 100, KeyboardManager.typed);
                    }
                    else {
                        // Trending
                        ModFileManager.QueueTrending(trendingOffset * 100);
                    }
                }
            }

            if (pageNumber > maxPages) {
                pageNumber = maxPages;
                
            }

            if (selectedPanel == Panels.FILES) {
                if (chosenSort != InstalledSort.BLACKLIST)
                {
                    PopulateFiles(pageNumber);
                }
                else {
                    PopulateBlacklist(pageNumber);
                }
            }

            if (selectedPanel == Panels.MODIO)
            {
                PopulateModIoTab(pageNumber);
            }

            if (selectedPanel == Panels.SETTINGS)
            {
                PopulateSettings(pageNumber);
            }

            UpdateArrowDisplays();
        }

        private void RegisterWholeKeyboard() {
            // HILARIOUS KEYBOARD HAHAHA 
            // Swipez can you add a keyboard??? Swipez can you??? Can you do this?? Can you do that???
            // Can I STRANGE YOU TO DEATH??? CAN I CHOKE YOU UNTIL YOU TURN BLUE AND EXPLODE????? MAYBE!!!! MAYBE I CAN!!!! HAHAHAHAHAHAHAHA
            // https://www.youtube.com/watch?v=eTrUEuUlsrw
            RegisterKey("Q");
            RegisterKey("W");
            RegisterKey("E");
            RegisterKey("R");
            RegisterKey("T");
            RegisterKey("Y");
            RegisterKey("U");
            RegisterKey("I");
            RegisterKey("O");
            RegisterKey("P");
            RegisterKey("A");
            RegisterKey("S");
            RegisterKey("D");
            RegisterKey("F");
            RegisterKey("G");
            RegisterKey("H");
            RegisterKey("J");
            RegisterKey("K");
            RegisterKey("L");
            RegisterKey("Z");
            RegisterKey("X");
            RegisterKey("C");
            RegisterKey("V");
            RegisterKey("B");
            RegisterKey("N");
            RegisterKey("M");
            RegisterKey("1");
            RegisterKey("2");
            RegisterKey("3");
            RegisterKey("4");
            RegisterKey("5");
            RegisterKey("6");
            RegisterKey("7");
            RegisterKey("8");
            RegisterKey("9");
            RegisterKey("0");
            RegisterKey(".");
            RegisterKey(",");
            RegisterKey("'");
            RegisterKey("-");
            RegisterKey("=");
            SetKeyAction("Backspace", () => { 
                KeyboardManager.Backspace();
            });
            SetKeyAction("Space", () => {
                KeyboardManager.Append(" ");
            });
            SetKeyAction("Enter", () => {
                Search(KeyboardManager.typed);
            });
            SetKeyAction("Exit", () => {
                rootAnimator.SetTrigger("keyboardpopup");
                SetMainCanvasColliderState(true);
            });
        }

        public void Reset() {
            transform.parent.Find("ModInfoOverlay").gameObject.SetActive(false);
            keyboardPopup.gameObject.SetActive(false);
            GetComponent<CanvasGroup>().interactable = true;
            SetMainCanvasColliderState(true);
        }

        private void RegisterKey(string keyName) {
            SetKeyAction(keyName, new Action(() => {
                KeyboardManager.Append(keyName);
            }));
        }

        private void PopupKeyboard() {
            rootAnimator.SetTrigger("keyboardpopup");
            SetMainCanvasColliderState(false);
        }

        private void SetKeyAction(string keyName, Action action)
        {
            GameObject key = keyboardPopup.transform.Find("Keyboard").Find(keyName).gameObject;
            Button button = key.GetComponent<Button>();
            button.onClick.AddListener(action);
        }

        // Update is called once per frame
        void Update()
        {
            List<StalledAction> toRemove = new List<StalledAction>();
            foreach (StalledAction stalledAction in stalledActions) {
                stalledAction.frameCount--;
                if (stalledAction.frameCount == 0) {
                    stalledAction.action.Invoke();
                    toRemove.Add(stalledAction);
                }
                
            }
            stalledActions.RemoveAll((stall) => toRemove.Contains(stall));
            if (desired != null && selector != null)
            {
                selector.position = Vector3.Lerp(selector.position, desired.position, speed * Time.deltaTime);
            }
            if (ModFileManager.activeDownloadQueueElement != null)
            {
                modProgressDisplay.SetActive(true);
                RawImage thumbnail = modProgressDisplay.transform.Find("Thumbnail").gameObject.GetComponent<RawImage>();
                if ((lastDownloadedTitle != ModFileManager.activeDownloadQueueElement.info.modName)) {
                    lastDownloadedTitle = ModFileManager.activeDownloadQueueElement.info.modName;
                    ThumbnailThreader.DownloadThumbnail(ModFileManager.activeDownloadQueueElement.info.thumbnailLink, (thumb =>
                    {
                        thumbnail.texture = thumb;
                    }));
                }
                TMP_Text title = modProgressDisplay.transform.Find("Title").gameObject.GetComponent<TMP_Text>();
                title.text = ModFileManager.activeDownloadQueueElement.info.modName;
                TMP_Text progress = modProgressDisplay.transform.Find("Percentage").gameObject.GetComponent<TMP_Text>();
                progress.text = ModlistMenu.activeDownloadModInfo.modDownloadPercentage + "%";
            }
            else {
                lastDownloadedTitle = "nothing";
                modProgressDisplay.SetActive(false);
            }

            if (multiplayerTabButton) {
                multiplayerTabButton.transform.parent.gameObject.SetActive(NetworkInfo.HasServer);
            }

            if (keyboardPopup) {
                typeBarText.text = KeyboardManager.typed;
                if (KeyboardManager.typed.IsEmpty())
                {
                    typeBarTextObject.SetActive(false);
                    typeBarEmptyTextObject.SetActive(true);
                }
                else {
                    typeBarTextObject.SetActive(true);
                    typeBarEmptyTextObject.SetActive(false);
                }
            }
        }

        public void SetSelectorDesired(Transform transform)
        {
            desired = transform;
        }
    }
}

public class StalledAction {
    public int frameCount;
    public Action action;
}
