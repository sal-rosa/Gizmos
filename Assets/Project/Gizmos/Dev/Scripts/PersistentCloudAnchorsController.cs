
#if !(ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER)
#error The cloud anchores sample needs Active Input Handling set to Both
#endif 

namespace Gizmos
{
    using System;
    using System.Collections.Generic;
    using Google.XR.ARCoreExtensions;
    using Unity.XR.CoreUtils;
    using UnityEngine;
    using UnityEngine.XR.ARFoundation;

    public class PersistentCloudAnchorsController : MonoBehaviour
    {
        [Header("AR Foundation")]

        public XROrigin Origin;

        public ARSession SessionCore;

        public ARCoreExtensions Extensions;

        public ARAnchorManager AnchorManager;

        public ARPlaneManager PlaneManager;

        public ARRaycastManager RaycastManager;

        [Header("UI")]
        public GameObject HomePage;

        public GameObject ResolveMenu;

        public GameObject PrivacyPrompt;

        public GameObject ARView;

        [HideInInspector]
        public ApplicationMode Mode = ApplicationMode.Ready;

        public HashSet<string> ResolvingSet = new HashSet<string>();

        private const string _hasDisplayedStartInfoKey = "HasDisplayedStartInfo";

        private const string _persistentCloudAnchorsStorageKey = "PersistentCloudAnchors";

        private const int _storageLimit = 40;

        public enum ApplicationMode
        {
            Ready,
            Hosting,
            Resolving,
        }

      
        public Camera MainCamera
        {
            get
            {
                return Origin.Camera;
            }
        }

        public void OnHostButtonClicked()
        {
            Mode = ApplicationMode.Hosting;
            SwitchToPrivacyPrompt();
        }

        public void OnResolveButtonClicked()
        {
            Mode = ApplicationMode.Resolving;
            SwitchToResolveMenu();
        }

        public void OnLearnMoreButtonClicked()
        {
            Application.OpenURL(
                "https://developers.google.com/ar/data-privacy");
        }

        public void SwitchToHomePage()
        {
            ResetAllViews();
            Mode = ApplicationMode.Ready;
            ResolvingSet.Clear();
            HomePage.SetActive(true);
        }

        public void SwitchToResolveMenu()
        {
            ResetAllViews();
            ResolveMenu.SetActive(true);
        }

        public void SwitchToPrivacyPrompt()
        {
            if (PlayerPrefs.HasKey(_hasDisplayedStartInfoKey))
            {
                SwitchToARView();
                return;
            }

            ResetAllViews();
            PrivacyPrompt.SetActive(true);
        }

        public void SwitchToARView()
        {
            ResetAllViews();
            PlayerPrefs.SetInt(_hasDisplayedStartInfoKey, 1);
            ARView.SetActive(true);
            SetPlatformActive(true);
        }

        public CloudAnchorHistoryCollection LoadCloudAnchorHistory()
        {
            if (PlayerPrefs.HasKey(_persistentCloudAnchorsStorageKey))
            {
                var history = JsonUtility.FromJson<CloudAnchorHistoryCollection>(
                    PlayerPrefs.GetString(_persistentCloudAnchorsStorageKey));

                DateTime current = DateTime.Now;
                history.Collection.RemoveAll(
                    data => current.Subtract(data.CreatedTime).Days > 0);
                PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey,
                    JsonUtility.ToJson(history));
                return history;
            }

            return new CloudAnchorHistoryCollection();
        }

        public void SaveCloudAnchorHistory(CloudAnchorHistory data)
        {
            var history = LoadCloudAnchorHistory();

            history.Collection.Add(data);
            history.Collection.Sort((left, right) => right.CreatedTime.CompareTo(left.CreatedTime));

            if (history.Collection.Count > _storageLimit)
            {
                history.Collection.RemoveRange(
                    _storageLimit, history.Collection.Count - _storageLimit);
            }

            PlayerPrefs.SetString(_persistentCloudAnchorsStorageKey, JsonUtility.ToJson(history));
        }

        public void Awake()
        {
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.orientation = ScreenOrientation.Portrait;

            Application.targetFrameRate = 60;
            SwitchToHomePage();
        }

        public void Update()
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                if (HomePage.activeSelf)
                {
                    Application.Quit();
                }
                else
                {
                    SwitchToHomePage();
                }
            }
        }

        private void ResetAllViews()
        {
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            SetPlatformActive(false);
            ARView.SetActive(false);
            PrivacyPrompt.SetActive(false);
            ResolveMenu.SetActive(false);
            HomePage.SetActive(false);
        }

        private void SetPlatformActive(bool active)
        {
            Origin.gameObject.SetActive(active);
            SessionCore.gameObject.SetActive(active);
            Extensions.gameObject.SetActive(active);
        }
    }
}
