namespace Gizmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Google.XR.ARCoreExtensions;
    using TMPro;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;
    using UnityEngine.XR.ARFoundation;
    using UnityEngine.XR.ARSubsystems;

    public class ARViewManager : MonoBehaviour
    {
        public PersistentCloudAnchorsController Controller;

        public GameObject CloudAnchorPrefab;

        public GameObject MapQualityIndicatorPrefab;

        public GameObject InstructionBar;

        public GameObject NamePanel;

        public GameObject DeletePanel;

        public GameObject EnvironmentPanel;

        public GameObject InputFieldWarning;

        public InputField NameField;

        public Text InstructionText;

        public Text TrackingHelperText;

        public Text DebugText;

        public GameObject Actions;

        public Button SaveButton;

        public Button ShareButton;

        public GameObject TargetIndicator;

        public GameObject ObjectToInstantiatePrefabDangerNote;

        public GameObject ObjectToInstantiatePrefabWarningNote;

        public GameObject ObjectToInstantiatePrefabInfoNote;

        public GameObject NotePanel;

        public GameObject SelectEnvironmentPanel;

        public InputField NoteInputField;

        public Button SaveNoteButton;

        public InputField EnvironmentInputField;

        public Button SaveEnvironmentButton;

        public Image NoteToInstantiate;

        public TMP_Dropdown EnvironmentDropdown;

        public Button SelectEnvironmentButton;

        private const string _initializingMessage = "O rastreamento está sendo inicializado.";

        private const string _relocalizingMessage = "O rastreamento está sendo retomado após uma interrupção.";

        private const string _insufficientLightMessage = "Muito escuro. Tente ir para uma área bem iluminada.";

        private const string _insufficientLightMessageAndroidS =
            "Muito escuro. Tente ir para uma área bem iluminada." +
            "Além disso, verifique se a câmera está habilitada para esse aplicativo";

        private const string _insufficientFeatureMessage =
            "Não consigo encontrar nada. Aponte o dispositivo para uma superfície com mais textura ou cor.";

        private const string _excessiveMotionMessage = "Muito rápido. Diminua a velocidade.";

        private const string _unsupportedMessage = "O motivo da perda de rastreamento não é suportado.";

        private const float _startPrepareTime = 3.0f;

        private const int _androidSSDKVesion = 31;

        private const string _pixelModel = "pixel";

        private float _timeSinceStart;

        private bool _isReturning;

        private List<Transform> _anchorsResolved = new List<Transform>();

        private MapQualityIndicator _qualityIndicator = null;
        private CloudAnchorHistory _hostedCloudAnchor;

        private ARAnchor _anchor = null;

        private bool _hasEnvironment = false;

        private HostCloudAnchorPromise _hostPromise = null;

        private HostCloudAnchorResult _hostResult = null;

        private IEnumerator _hostCoroutine = null;

        private List<ResolveCloudAnchorPromise> _resolvePromises =
            new List<ResolveCloudAnchorPromise>();

        private List<ResolveCloudAnchorResult> _resolveResults =
            new List<ResolveCloudAnchorResult>();

        private List<IEnumerator> _resolveCoroutines = new List<IEnumerator>();

        private Color _activeColor;

        private AndroidJavaClass _versionInfo;

        private Vector3 _nextPositionToObjectInstantiate;

        private GameObject _nextObjectToInstantiate;

        private string _nextTypeOfNote;

        private bool _mapingVisible = true;

        private string _selectedNote;

        private class ObjectNotePositionData
        {
            public string noteId;
            public float posX;
            public float posY;
            public float posZ;
            public string text;
            public string noteType;
            public string anchorId;
        }

        private class ObjectInfraPositionData
        {
            public string infraId;
            public float posX;
            public float posY;
            public float posZ;
            public string text;
            public string anchorId;
        }


        private List<ObjectNotePositionData> _savedNotePositions = new List<ObjectNotePositionData>();

        private List<ObjectInfraPositionData> _savedInfraPositions = new List<ObjectInfraPositionData>();


        private List<string> _environmentList = new List<string>();

        private string _selectedEnvironment;

        private Dictionary<string, Transform> _anchorMap = new Dictionary<string, Transform>();

        private HashSet<string> _restoredNotes = new HashSet<string>();

        private Transform _anchorTransform;

        public Pose GetCameraPose()
        {
            return new Pose(Controller.MainCamera.transform.position,
                Controller.MainCamera.transform.rotation);
        }

        public void OnInputFieldValueChanged(string inputString)
        {
            var regex = new Regex("^[a-zA-Z0-9-_]*$");
            InputFieldWarning.SetActive(!regex.IsMatch(inputString));
            SetSaveButtonActive(!InputFieldWarning.activeSelf && inputString.Length > 0);
        }

        public void OnSaveButtonClicked()
        {
            _hostedCloudAnchor.Name = NameField.text;
            Controller.SaveCloudAnchorHistory(_hostedCloudAnchor);

            DebugText.text = string.Format("Âncora salva:\n{0}.", _hostedCloudAnchor.Name);
            ShareButton.gameObject.SetActive(true);
            NamePanel.SetActive(false);

            string savedEnvironments = PlayerPrefs.GetString("SavedEnvironments", "");
            if (string.IsNullOrEmpty(savedEnvironments))
            {
                Debug.LogWarning("Nenhum ambiente foi criado ainda. Crie um ambiente antes de associar IDs.");
                return;
            }

            List<string> environmentList = new List<string>(savedEnvironments.Split(';'));
            string lastEnvironmentName = environmentList[environmentList.Count - 1];

            string newAnchorId = _hostedCloudAnchor.Id;
            if (string.IsNullOrEmpty(newAnchorId))
            {
                Debug.LogWarning("O ID da âncora está vazio. Não é possível salvar.");
                return;
            }

            if (_selectedEnvironment.Length > 0)
            {
                lastEnvironmentName = _selectedEnvironment;
            }

            string existingIds = PlayerPrefs.GetString(lastEnvironmentName, "");
            List<string> idList = new List<string>(existingIds.Split(','));
            if (!idList.Contains(newAnchorId))
            {
                idList.Add(newAnchorId);
            }

            PlayerPrefs.SetString(lastEnvironmentName, string.Join(",", idList));
            PlayerPrefs.Save();

            Debug.Log($"ID {newAnchorId} associado ao ambiente {lastEnvironmentName}");
        }

        public void OnShareButtonClicked()
        {
            GUIUtility.systemCopyBuffer = _hostedCloudAnchor.Id;
            DebugText.text = "Copied cloud id: " + _hostedCloudAnchor.Id;
        }

        public void Awake()
        {
            _activeColor = SaveButton.GetComponentInChildren<Text>().color;
            _versionInfo = new AndroidJavaClass("android.os.Build$VERSION");

            LoadEnvironments();
        }

        public void OnEnable()
        {
            _timeSinceStart = 0.0f;
            _isReturning = false;
            _anchor = null;
            _qualityIndicator = null;
            _hostPromise = null;
            _hostResult = null;
            _hostCoroutine = null;
            _hasEnvironment = false;
            _resolvePromises.Clear();
            _resolveResults.Clear();
            _resolveCoroutines.Clear();

            InstructionBar.SetActive(true);
            NamePanel.SetActive(false);
            InputFieldWarning.SetActive(false);
            ShareButton.gameObject.SetActive(false);
            NotePanel.SetActive(false);
            UpdatePlaneVisibility(true);

            switch (Controller.Mode)
            {
                case PersistentCloudAnchorsController.ApplicationMode.Ready:
                    ReturnToHomePage("Modo de aplicação inválido. Retornando à página inicial...");
                    break;
                case PersistentCloudAnchorsController.ApplicationMode.Hosting:
                    Actions.SetActive(false);
                    EnvironmentPanel.SetActive(true);
                    SelectEnvironmentPanel.SetActive(false);
                    InstructionText.text = "Detectando superfície plana...";
                    DebugText.text = "O ARCore está se preparando para " + Controller.Mode;
                    break;
                case PersistentCloudAnchorsController.ApplicationMode.Resolving:
                    SelectEnvironmentPanel.SetActive(true);
                    EnvironmentPanel.SetActive(false);
                    InstructionText.text = "Detectando superfície plana...";
                    DebugText.text = "O ARCore está se preparando para " + Controller.Mode;
                    break;
            }
        }

        public void OnDisable()
        {
            if (_qualityIndicator != null)
            {
                Destroy(_qualityIndicator.gameObject);
                _qualityIndicator = null;
            }

            if (_anchor != null)
            {
                Destroy(_anchor.gameObject);
                _anchor = null;
            }

            if (_hostCoroutine != null)
            {
                StopCoroutine(_hostCoroutine);
            }

            _hostCoroutine = null;

            if (_hostPromise != null)
            {
                _hostPromise.Cancel();
                _hostPromise = null;
            }

            _hostResult = null;

            foreach (var coroutine in _resolveCoroutines)
            {
                StopCoroutine(coroutine);
            }

            _resolveCoroutines.Clear();

            foreach (var promise in _resolvePromises)
            {
                promise.Cancel();
            }

            _resolvePromises.Clear();

            foreach (var result in _resolveResults)
            {
                if (result.Anchor != null)
                {
                    Destroy(result.Anchor.gameObject);
                }
            }

            GameObject[] notes = GameObject.FindGameObjectsWithTag("Note");
            foreach (GameObject note in notes)
            {
                Destroy(note);
            }

            LineRenderer[] lineRenderers = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None);
            foreach (LineRenderer lr in lineRenderers)
            {
                Destroy(lr.gameObject);
            }

            _resolveResults.Clear();
            UpdatePlaneVisibility(false);
        }

        public void Update()
        {
            List<ARRaycastHit> hits = new List<ARRaycastHit>();

            if (Controller.RaycastManager.Raycast(new Vector2(Screen.width / 2, Screen.height / 2), hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinBounds))
            {
                TargetIndicator.SetActive(true);

                Vector3 hitPosition = hits[0].pose.position;
                Vector3 planeNormal = hits[0].pose.up;

                float distanceFromSurface = 0.1f;
                Vector3 positionOffset = planeNormal * distanceFromSurface;

                TargetIndicator.transform.position = hitPosition + positionOffset;

                TargetIndicator.transform.rotation = Quaternion.FromToRotation(Vector3.forward, planeNormal);
            }
            else
            {
                TargetIndicator.SetActive(false);
            }

            if (_timeSinceStart < _startPrepareTime)
            {
                _timeSinceStart += Time.deltaTime;
                if (_timeSinceStart >= _startPrepareTime)
                {
                    UpdateInitialInstruction();
                }

                return;
            }

            ARCoreLifecycleUpdate();
            if (_isReturning)
            {
                return;
            }

            if (_timeSinceStart >= _startPrepareTime)
            {
                DisplayTrackingHelperMessage();
            }

            if (Controller.Mode == PersistentCloudAnchorsController.ApplicationMode.Resolving)
            {
                ResolvingCloudAnchors();

                Touch touch;
                if (Input.touchCount < 1 ||
                    (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                {
                    return;
                }

                if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                {
                    return;
                }

                Ray ray = Camera.main.ScreenPointToRay(touch.position);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit))
                {
                    GameObject touchedObject = hit.transform.gameObject;

                    if (touchedObject.CompareTag("Note"))
                    {
                        _selectedNote = touchedObject.name.Replace("object", ""); ;
                        DeletePanel.SetActive(true);
                    }
                }
            }
            else if (Controller.Mode == PersistentCloudAnchorsController.ApplicationMode.Hosting)
            {
                if (_anchor == null && _hasEnvironment)
                {
                    Touch touch;
                    if (Input.touchCount < 1 ||
                        (touch = Input.GetTouch(0)).phase != TouchPhase.Began)
                    {
                        return;
                    }

                    if (EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                    {
                        return;
                    }

                    PerformHitTest(touch.position);
                }

                HostingCloudAnchor();
            }
        }

        private void PerformHitTest(Vector2 touchPos)
        {
            List<ARRaycastHit> hitResults = new List<ARRaycastHit>();
            Controller.RaycastManager.Raycast(
                touchPos, hitResults, TrackableType.PlaneWithinPolygon);

            var planeType = PlaneAlignment.HorizontalUp;
            if (hitResults.Count > 0)
            {
                ARPlane plane = Controller.PlaneManager.GetPlane(hitResults[0].trackableId);
                if (plane == null)
                {
                    Debug.LogWarningFormat("Falha ao encontrar o ARPlane com TrackableId {0}.",
                        hitResults[0].trackableId);
                    return;
                }

                planeType = plane.alignment;
                var hitPose = hitResults[0].pose;

                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    hitPose.rotation.eulerAngles =
                        new Vector3(0.0f, Controller.MainCamera.transform.eulerAngles.y, 0.0f);
                }

                _anchor = Controller.AnchorManager.AttachAnchor(plane, hitPose);
            }

            if (_anchor != null)
            {
                float distanceFromSurface = 0.1f;
                Vector3 positionOffset = Vector3.up * distanceFromSurface;

                var cloudAnchorInstance = Instantiate(CloudAnchorPrefab, _anchor.transform);
                cloudAnchorInstance.transform.position += positionOffset;

                var indicatorGO = Instantiate(MapQualityIndicatorPrefab, _anchor.transform);
                _qualityIndicator = indicatorGO.GetComponent<MapQualityIndicator>();
                _qualityIndicator.DrawIndicator(planeType, Controller.MainCamera);

                InstructionText.text = "Para salvar a âncora, caminhe ao redor do objeto para" +
                    "capturá-lo de diferentes ângulos";
                DebugText.text = "Aguardando qualidade de mapeamento suficiente...";

                UpdatePlaneVisibility(false);
            }
        }

        private void HostingCloudAnchor()
        {
            if (_anchor == null)
            {
                return;
            }

            if (_hostPromise != null || _hostResult != null)
            {
                return;
            }

            int qualityState = 2;

            FeatureMapQuality quality =
                Controller.AnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose());
            DebugText.text = "Qualidade de mapeamento atual: " + quality;
            qualityState = (int)quality;
            _qualityIndicator.UpdateQualityState(qualityState);

            var cameraDist = (_qualityIndicator.transform.position -
                Controller.MainCamera.transform.position).magnitude;
            if (cameraDist < _qualityIndicator.Radius * 1.5f)
            {
                InstructionText.text = "Você está muito perto, mova-se para trás.";
                return;
            }
            else if (cameraDist > 10.0f)
            {
                InstructionText.text = "Você está muito longe, chegue mais perto.";
                return;
            }
            else if (_qualityIndicator.ReachTopviewAngle)
            {
                InstructionText.text =
                    "Você está olhando de cima, mova-se por todos os lados.";
                return;
            }
            else if (!_qualityIndicator.ReachQualityThreshold)
            {
                InstructionText.text = "Salve o objeto aqui capturando-o de todos os lados.";
                return;
            }

            InstructionText.text = "Processando...";
            DebugText.text = "A qualidade do mapeamento atingiu um limite suficiente, " +
                "criando âncora.";
            DebugText.text = string.Format(
                "Rastreamento chegou a {0}, criando âncora",
                Controller.AnchorManager.EstimateFeatureMapQualityForHosting(GetCameraPose()));

            var promise = Controller.AnchorManager.HostCloudAnchorAsync(_anchor, 1);
            if (promise.State == PromiseState.Done)
            {
                Debug.LogFormat("Falha ao criar âncora");
                OnAnchorHostedFinished(false);
            }
            else
            {
                _hostPromise = promise;
                _hostCoroutine = HostAnchor();
                StartCoroutine(_hostCoroutine);
            }
        }

        private IEnumerator HostAnchor()
        {
            yield return _hostPromise;
            _hostResult = _hostPromise.Result;
            _hostPromise = null;

            if (_hostResult.CloudAnchorState == CloudAnchorState.Success)
            {
                int count = Controller.LoadCloudAnchorHistory().Collection.Count;
                _hostedCloudAnchor =
                    new CloudAnchorHistory("CloudAnchor" + count, _hostResult.CloudAnchorId);
                OnAnchorHostedFinished(true, _hostResult.CloudAnchorId);
            }
            else
            {
                OnAnchorHostedFinished(false, _hostResult.CloudAnchorState.ToString());
            }
        }

        private void ResolvingCloudAnchors()
        {
            if (Controller.ResolvingSet.Count == 0)
            {
                return;
            }

            if (_selectedEnvironment == null)
            {
                return;
            }

            if (_resolvePromises.Count > 0 || _resolveResults.Count > 0)
            {
                return;
            }

            if (ARSession.state != ARSessionState.SessionTracking)
            {
                return;
            }

            Debug.LogFormat("Tentando buscar {0} âncora(s): {1}",
                Controller.ResolvingSet.Count,
                string.Join(",", new List<string>(Controller.ResolvingSet).ToArray()));

            string savedIds = PlayerPrefs.GetString(_selectedEnvironment, "");
            HashSet<string> validCloudIds = new HashSet<string>(savedIds.Split(','));

            foreach (string cloudId in Controller.ResolvingSet)
            {
                if (!validCloudIds.Contains(cloudId))
                {
                    Debug.Log($"Ignorando ID {cloudId}, pois não pertence ao ambiente {_selectedEnvironment}");
                    continue;
                }

                var promise = Controller.AnchorManager.ResolveCloudAnchorAsync(cloudId);
                if (promise.State == PromiseState.Done)
                {
                    Debug.LogFormat("Falha ao buscar ambiente " + cloudId);
                    OnAnchorResolvedFinished(false, cloudId);
                }
                else
                {
                    _resolvePromises.Add(promise);
                    var coroutine = ResolveAnchor(cloudId, promise);
                    StartCoroutine(coroutine);
                }
            }

            Controller.ResolvingSet.Clear();
        }

        private IEnumerator ResolveAnchor(string cloudId, ResolveCloudAnchorPromise promise)
        {
            yield return promise;
            var result = promise.Result;
            _resolvePromises.Remove(promise);
            _resolveResults.Add(result);

            if (result.CloudAnchorState == CloudAnchorState.Success)
            {
                OnAnchorResolvedFinished(true, cloudId);
                yield return new WaitForSeconds(2f);
                Transform anchorTransform = result.Anchor.transform;
                Instantiate(CloudAnchorPrefab, anchorTransform);
                _anchorsResolved.Add(anchorTransform);
                _anchorMap[cloudId] = anchorTransform;
                RestoreNoteObjects();
            }
            else
            {
                OnAnchorResolvedFinished(false, cloudId, result.CloudAnchorState.ToString());
            }
        }

        private void OnAnchorHostedFinished(bool success, string response = null)
        {
            if (success)
            {
                InstructionText.text = "Finalizado!";
                Invoke("DoHideInstructionBar", 1.5f);
                DebugText.text =
                    string.Format("Sucesso ao criar âncora: {0}.", response);

                NameField.text = _hostedCloudAnchor.Name;
                NamePanel.SetActive(true);
                SetSaveButtonActive(true);
            }
            else
            {
                InstructionText.text = "Falha ao criar âncora.";
                DebugText.text = "Falha ao criar âncora" + (response == null ? "." :
                    "com erro " + response + ".");
            }
        }

        private void OnAnchorResolvedFinished(bool success, string cloudId, string response = null)
        {
            if (success)
            {
                InstructionText.text = "Âncora encontrada!";
                DebugText.text =
                    string.Format("Sucesso ao encontrar âncora: {0}.", cloudId);
            }
            else
            {
                InstructionText.text = "Buscar falhou.";
                DebugText.text = "Falha ao buscar âncora: " + cloudId +
                    (response == null ? "." : "com erro " + response + ".");
            }
        }

        private void UpdateInitialInstruction()
        {
            switch (Controller.Mode)
            {
                case PersistentCloudAnchorsController.ApplicationMode.Hosting:
                    InstructionText.text = "Toque para posicionar uma âncora.";
                    DebugText.text = "Toque em um plano vertical ou horizontal...";
                    return;
                case PersistentCloudAnchorsController.ApplicationMode.Resolving:
                    InstructionText.text =
                        "Observe o local onde você espera ver as informações";
                    DebugText.text = string.Format("Tentando buscar {0} âncoras...",
                        Controller.ResolvingSet.Count);
                    return;
                default:
                    return;
            }
        }

        private void UpdatePlaneVisibility(bool visible)
        {
            foreach (var plane in Controller.PlaneManager.trackables)
            {
                plane.gameObject.SetActive(visible);
            }
        }

        public void DisableMapping()
        {
            _mapingVisible = !_mapingVisible;

            Controller.PlaneManager.enabled = _mapingVisible;

            foreach (var plane in Controller.PlaneManager.trackables)
            {
                plane.gameObject.SetActive(_mapingVisible);
            }
        }

        private void ARCoreLifecycleUpdate()
        {
            var sleepTimeout = SleepTimeout.NeverSleep;
            if (ARSession.state != ARSessionState.SessionTracking)
            {
                sleepTimeout = SleepTimeout.SystemSetting;
            }

            Screen.sleepTimeout = sleepTimeout;

            if (_isReturning)
            {
                return;
            }

            if (ARSession.state != ARSessionState.Ready &&
                ARSession.state != ARSessionState.SessionInitializing &&
                ARSession.state != ARSessionState.SessionTracking)
            {
                ReturnToHomePage(string.Format(
                    "O ARCore encontrou um estado de erro {0}. Por favor, reinicie o aplicativo.",
                    ARSession.state));
            }
        }

        private void DisplayTrackingHelperMessage()
        {
            if (_isReturning || ARSession.notTrackingReason == NotTrackingReason.None)
            {
                TrackingHelperText.gameObject.SetActive(false);
            }
            else
            {
                TrackingHelperText.gameObject.SetActive(true);
                switch (ARSession.notTrackingReason)
                {
                    case NotTrackingReason.Initializing:
                        TrackingHelperText.text = _initializingMessage;
                        return;
                    case NotTrackingReason.Relocalizing:
                        TrackingHelperText.text = _relocalizingMessage;
                        return;
                    case NotTrackingReason.InsufficientLight:
                        if (_versionInfo.GetStatic<int>("SDK_INT") < _androidSSDKVesion)
                        {
                            TrackingHelperText.text = _insufficientLightMessage;
                        }
                        else
                        {
                            TrackingHelperText.text = _insufficientLightMessageAndroidS;
                        }

                        return;
                    case NotTrackingReason.InsufficientFeatures:
                        TrackingHelperText.text = _insufficientFeatureMessage;
                        return;
                    case NotTrackingReason.ExcessiveMotion:
                        TrackingHelperText.text = _excessiveMotionMessage;
                        return;
                    case NotTrackingReason.Unsupported:
                        TrackingHelperText.text = _unsupportedMessage;
                        return;
                    default:
                        TrackingHelperText.text =
                            string.Format("Motivo do não rastreamento: {0}", ARSession.notTrackingReason);
                        return;
                }
            }
        }

        private void ReturnToHomePage(string reason)
        {
            Debug.Log("Voltando para tela inicial pelo motivo:" + reason);
            if (_isReturning)
            {
                return;
            }

            _isReturning = true;
            DebugText.text = reason;
            Invoke("DoReturnToHomePage", 3.0f);
        }

        public void SaveNote()
        {

            string noteInputField = NoteInputField.text;

            GameObject newObject = Instantiate(_nextObjectToInstantiate, _nextPositionToObjectInstantiate, Quaternion.identity);

            string noteId = System.Guid.NewGuid().ToString();

            newObject.name = noteId;

            if (noteInputField.Length > 0)
            {
                GameObject canvasObject = GameObject.Find("Canvas3D");

                if (canvasObject != null)
                {
                    Image newNote = Instantiate(NoteToInstantiate, canvasObject.transform, false);
                    newNote.name = noteId;

                    Renderer mainRenderer = newObject.GetComponent<Renderer>();
                    float offsetY = mainRenderer.bounds.size.y + 0.1f;

                    newNote.transform.position = newObject.transform.position + new Vector3(0, offsetY, 0);
                    newNote.transform.rotation = Quaternion.Euler(0, 180, 0);

                    TMP_Text tmpText = newNote.GetComponentInChildren<TMP_Text>();
                    if (tmpText != null)
                    {
                        tmpText.text = noteInputField;
                        tmpText.rectTransform.Rotate(0, 180, 0);
                    }
                }
                else
                {
                    Debug.LogError("Canvas não encontrado ou prefab da nota não atribuído!");
                }
            }

            List<string> savedNoteKeys = GetSavedNoteKeys();

            foreach (Transform anchorTransform in _anchorsResolved)
            {
                string anchorId = _anchorMap.FirstOrDefault(x => x.Value == anchorTransform).Key;
                if (string.IsNullOrEmpty(anchorId))
                {
                    Debug.LogWarning("Nenhum ID de âncora correspondente encontrado.");
                    continue;
                }

                string uniqueKey = "ObjectNote_" + _selectedEnvironment + "_" + noteId + "_" + anchorId;

                Vector3 relativePosition = anchorTransform.InverseTransformPoint(newObject.transform.position);

                ObjectNotePositionData data = new ObjectNotePositionData
                {
                    noteId = noteId,
                    posX = relativePosition.x,
                    posY = relativePosition.y,
                    posZ = relativePosition.z,
                    text = noteInputField,
                    noteType = _nextTypeOfNote,
                    anchorId = anchorId
                };

                string jsonData = JsonUtility.ToJson(data);

                if (PlayerPrefs.HasKey(uniqueKey))
                {
                    Debug.LogWarning($"Nota com a chave {uniqueKey} já existe, ignorando duplicação.");
                    continue;
                }

                savedNoteKeys.Add(uniqueKey);
                PlayerPrefs.SetString("SavedNoteKeys_" + _selectedEnvironment, string.Join(";", savedNoteKeys));
                PlayerPrefs.SetString(uniqueKey, jsonData);
                _savedNotePositions.Add(data);
                PlayerPrefs.Save();
            }

            NotePanel.SetActive(false);
        }

        private List<string> GetSavedNoteKeys()
        {
            string savedNoteKeysString = PlayerPrefs.GetString("SavedNoteKeys_" + _selectedEnvironment, "");
            if (string.IsNullOrEmpty(savedNoteKeysString))
            {
                return new List<string>();
            }

            return new List<string>(savedNoteKeysString.Split(';'));
        }

        public void SaveAllNotesFromList()
        {
            if (_noteAnchorDataList == null || _noteAnchorDataList.Count == 0)
            {
                Debug.LogWarning("Nenhuma nota para salvar.");
                return;
            }

            List<string> savedNoteKeys = GetSavedNoteKeys();

            foreach (var noteData in _noteAnchorDataList)
            {
                foreach (Transform anchorTransform in _anchorsResolved)
                {
                    string anchorId = _anchorMap.FirstOrDefault(x => x.Value == anchorTransform).Key;

                    if (string.IsNullOrEmpty(anchorId))
                    {
                        Debug.LogWarning("AnchorId não encontrado, pulando.");
                        continue;
                    }

                    string uniqueKey = "ObjectNote_" + _selectedEnvironment + "_" + noteData.noteId + "_" + anchorId;

                    if (PlayerPrefs.HasKey(uniqueKey))
                    {
                        Debug.Log($"Nota '{noteData.noteId}' já salva para âncora '{anchorId}', pulando.");
                        continue;
                    }

                    Vector3 relativePos = anchorTransform.InverseTransformPoint(noteData.anchorPosition);

                    ObjectNotePositionData data = new ObjectNotePositionData
                    {
                        noteId = noteData.noteId,
                        posX = relativePos.x,
                        posY = relativePos.y,
                        posZ = relativePos.z,
                        text = noteData.noteText,
                        noteType = noteData.noteType,
                        anchorId = anchorId
                    };

                    string jsonData = JsonUtility.ToJson(data);

                    savedNoteKeys.Add(uniqueKey);
                    PlayerPrefs.SetString("SavedNoteKeys_" + _selectedEnvironment, string.Join(";", savedNoteKeys));
                    PlayerPrefs.SetString(uniqueKey, jsonData);
                    _savedNotePositions.Add(data);
                }
            }

            PlayerPrefs.Save();
            Debug.Log("Notas salvas para todas as âncoras (exceto duplicadas).");
        }


        public void PositionNoteObject(GameObject objectPrefab, string nextTypeOfNote)
        {
            List<ARRaycastHit> hits = new List<ARRaycastHit>();

            Vector3 forwardPosition = Camera.main.transform.position + Camera.main.transform.forward * 0.5f;

            if (Controller.RaycastManager.Raycast(new Vector2(Screen.width / 2, Screen.height / 2), hits, UnityEngine.XR.ARSubsystems.TrackableType.PlaneWithinBounds))
            {
                Pose hitPose = hits[0].pose;
                _nextPositionToObjectInstantiate = hitPose.position;
            }
            else
            {
                _nextPositionToObjectInstantiate = forwardPosition;
            }

            _nextObjectToInstantiate = objectPrefab;
            _nextTypeOfNote = nextTypeOfNote;
            NotePanel.SetActive(true);
        }

        public void PositionObjectNoteDanger() => PositionNoteObject(ObjectToInstantiatePrefabDangerNote, "danger");
        public void PositionObjectNoteWarning() => PositionNoteObject(ObjectToInstantiatePrefabWarningNote, "warning");
        public void PositionObjectNoteInfo() => PositionNoteObject(ObjectToInstantiatePrefabInfoNote, "info");

        [System.Serializable]
        public class NoteAnchorData
        {
            public Vector3 anchorPosition;
            public string noteType;
            public string noteText;
            public Vector3 relativePosition;
            public string noteId;

            public NoteAnchorData(Vector3 anchorPos, string type, string text, Vector3 relativePos, string id)
            {
                anchorPosition = anchorPos;
                noteType = type;
                noteText = text;
                relativePosition = relativePos;
                noteId = id;
            }
        }

        private List<NoteAnchorData> _noteAnchorDataList = new List<NoteAnchorData>();

        private async void RestoreNoteObjects()
        {
            string savedEnvironments = PlayerPrefs.GetString("SavedEnvironments", "");
            if (string.IsNullOrEmpty(savedEnvironments))
            {
                Debug.LogWarning("Nenhum ambiente salvo encontrado.");
                return;
            }

            List<string> environmentList = new List<string>(savedEnvironments.Split(';'));
            string lastEnvironmentName = environmentList[environmentList.Count - 1];

            string associatedAnchors = PlayerPrefs.GetString(lastEnvironmentName, "");
            List<string> anchorIds = new List<string>(associatedAnchors.Split(','));

            List<string> savedNoteKeys = GetSavedNoteKeys();

            foreach (string anchorId in anchorIds)
            {
                if (!_anchorMap.ContainsKey(anchorId))
                {
                    Debug.LogWarning($"Âncora {anchorId} não encontrada.");
                    continue;
                }

                Transform anchorTransform = _anchorMap[anchorId];

                foreach (string uniqueKey in savedNoteKeys)
                {
                    if (uniqueKey.Contains(anchorId))
                    {
                        if (PlayerPrefs.HasKey(uniqueKey))
                        {
                            string jsonData = PlayerPrefs.GetString(uniqueKey);
                            ObjectNotePositionData data = JsonUtility.FromJson<ObjectNotePositionData>(jsonData);

                            string uniqueKeyNote = "ObjectNote_" + _selectedEnvironment + "_" + data.noteId + "_" + anchorId;

                            if (_restoredNotes.Contains(uniqueKeyNote))
                            {
                                Debug.Log($"Nota {uniqueKey} já restaurada, ignorando duplicação.");
                                continue;
                            }

                            Vector3 relativePosition = new Vector3(data.posX, data.posY, data.posZ);
                            Vector3 globalPosition = anchorTransform.TransformPoint(relativePosition);

                            GameObject mainObject = null;

                            ARAnchor anchor = null;

                            if (data.noteType == "danger")
                            {
                                var pose = new Pose(globalPosition, Quaternion.identity);
                                var result = await Controller.AnchorManager.TryAddAnchorAsync(pose);
                                if (result.status.IsSuccess())
                                {
                                    anchor = result.value;
                                    if (anchor != null)
                                    {
                                        _noteAnchorDataList.Add(new NoteAnchorData(
      anchor.transform.position,
      data.noteType,
      data.text,
      new Vector3(data.posX, data.posY, data.posZ),
      data.noteId
  ));
                                        mainObject = Instantiate(ObjectToInstantiatePrefabDangerNote, anchor.transform);
                                        mainObject.name = data.noteId;
                                    }
                                }
                                else
                                {
                                    Debug.LogError("Falha ao criar a âncora!");
                                }
                            }
                            else if (data.noteType == "warning")
                            {
                                var pose = new Pose(globalPosition, Quaternion.identity);
                                var result = await Controller.AnchorManager.TryAddAnchorAsync(pose);
                                if (result.status.IsSuccess())
                                {
                                    anchor = result.value;
                                    if (anchor != null)
                                    {
                                        _noteAnchorDataList.Add(new NoteAnchorData(
      anchor.transform.position,
      data.noteType,
      data.text,
      new Vector3(data.posX, data.posY, data.posZ),
      data.noteId
  ));
                                        mainObject = Instantiate(ObjectToInstantiatePrefabWarningNote, anchor.transform);
                                        mainObject.name = data.noteId;
                                    }
                                }
                                else
                                {
                                    Debug.LogError("Falha ao criar a âncora!");
                                }
                            }
                            else if (data.noteType == "info")
                            {
                                var pose = new Pose(globalPosition, Quaternion.identity);
                                var result = await Controller.AnchorManager.TryAddAnchorAsync(pose);
                                if (result.status.IsSuccess())
                                {
                                    anchor = result.value;
                                    if (anchor != null)
                                    {
                                        _noteAnchorDataList.Add(new NoteAnchorData(
      anchor.transform.position,
      data.noteType,
      data.text,
      new Vector3(data.posX, data.posY, data.posZ),
      data.noteId
  ));
                                        mainObject = Instantiate(ObjectToInstantiatePrefabInfoNote, anchor.transform);
                                        mainObject.name = data.noteId;
                                    }
                                }
                                else
                                {
                                    Debug.LogError("Falha ao criar a âncora!");
                                }
                            }

                            if (mainObject != null)
                            {
                                _restoredNotes.Add(uniqueKeyNote);
                                DrawSmartCurve(anchorTransform.position, mainObject.transform.position, data.noteId);

                                if (data.text.Length > 0)
                                {
                                    GameObject canvasObject = GameObject.Find("Canvas3D");

                                    if (canvasObject != null)
                                    {
                                        Image newNote = Instantiate(NoteToInstantiate, canvasObject.transform, false);
                                        newNote.name = data.noteId;

                                        Renderer mainRenderer = mainObject.GetComponent<Renderer>();
                                        float offsetY = mainRenderer.bounds.size.y + 0.1f;

                                        newNote.transform.position = mainObject.transform.position + new Vector3(0, offsetY, 0);
                                        newNote.transform.rotation = Quaternion.Euler(0, 180, 0);

                                        TMP_Text tmpText = newNote.GetComponentInChildren<TMP_Text>();
                                        if (tmpText != null)
                                        {
                                            tmpText.text = data.text;
                                            tmpText.rectTransform.Rotate(0, 180, 0);
                                        }
                                    }
                                    else
                                    {
                                        Debug.LogError("Canvas não encontrado ou prefab da nota não atribuído!");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DrawSmartCurve(Vector3 start, Vector3 end, string name)
        {
            GameObject lineObject = new GameObject("SmartCurve");
            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();

            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = Color.white;
            lineRenderer.startWidth = 0.02f;
            lineRenderer.endWidth = 0.02f;
            lineRenderer.positionCount = 20;

            Vector3[] points;
            if (NeedsCurve(start, end))
            {
                points = GenerateAdaptiveCurve(start, end, 20);
            }
            else
            {
                points = new Vector3[] { start, end };
                lineRenderer.positionCount = 2;
            }

            lineRenderer.SetPositions(points);
            lineObject.name = name;

        }

        private bool NeedsCurve(Vector3 start, Vector3 end)
        {
            float heightDifference = Mathf.Abs(start.y - end.y);
            float distance = Vector3.Distance(start, end);

            return heightDifference > 0.2f && distance > 0.3f;
        }

        private Vector3[] GenerateAdaptiveCurve(Vector3 start, Vector3 end, int resolution)
        {
            Vector3[] curvePoints = new Vector3[resolution];

            Vector3 midPoint = (start + end) / 2;
            Vector3 controlPoint1, controlPoint2;

            if (start.y < end.y)
            {
                controlPoint1 = new Vector3(midPoint.x, start.y, midPoint.z);
                controlPoint2 = new Vector3(midPoint.x, end.y, midPoint.z);
            }
            else
            {
                controlPoint1 = new Vector3(midPoint.x, end.y, midPoint.z);
                controlPoint2 = new Vector3(midPoint.x, start.y, midPoint.z);
            }

            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                curvePoints[i] = Mathf.Pow(1 - t, 3) * start +
                                 3 * Mathf.Pow(1 - t, 2) * t * controlPoint1 +
                                 3 * (1 - t) * Mathf.Pow(t, 2) * controlPoint2 +
                                 Mathf.Pow(t, 3) * end;
            }

            return curvePoints;
        }

        public void DeleteNote()
        {
            GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (GameObject obj in allObjects)
            {
                if (obj.name == _selectedNote)
                {
                    Destroy(obj);
                }
            }

            List<string> savedNoteKeys = GetSavedNoteKeys();
            List<string> keysToRemove = new List<string>();

            foreach (string key in savedNoteKeys)
            {
                if (key.Contains("_" + _selectedNote + "_"))
                {
                    PlayerPrefs.DeleteKey(key);
                    keysToRemove.Add(key);

                    var data = _savedNotePositions.FirstOrDefault(d => d.noteId == _selectedNote);
                    if (data != null)
                        _savedNotePositions.Remove(data);
                }
            }

            foreach (string key in keysToRemove)
            {
                savedNoteKeys.Remove(key);
            }

            PlayerPrefs.SetString("SavedNoteKeys_" + _selectedEnvironment, string.Join(";", savedNoteKeys));
            PlayerPrefs.Save();
            DeletePanel.SetActive(false);
        }

        public void SaveEnvironmentName()
        {
            string environmentName = EnvironmentInputField.text;
            string uniqueEnvironmentName = environmentName + "_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string existingEnvironments = PlayerPrefs.GetString("SavedEnvironments", "");

            if (!string.IsNullOrEmpty(existingEnvironments))
            {
                existingEnvironments += ";" + uniqueEnvironmentName;
            }
            else
            {
                existingEnvironments = uniqueEnvironmentName;
            }

            PlayerPrefs.SetString("SavedEnvironments", existingEnvironments);
            PlayerPrefs.Save();

            EnvironmentPanel.SetActive(false);
            _hasEnvironment = true;
            Debug.Log("Novo ambiente salvo: " + uniqueEnvironmentName);
        }

        private void LoadEnvironments()
        {
            EnvironmentDropdown.ClearOptions();

            string savedEnvironments = PlayerPrefs.GetString("SavedEnvironments", "");
            Debug.Log($"Ambientes carregados do PlayerPrefs: {savedEnvironments}");

            if (!string.IsNullOrEmpty(savedEnvironments))
            {
                _environmentList = new List<string>(savedEnvironments.Split(';'));

                if (_environmentList.Count > 0)
                {
                    EnvironmentDropdown.AddOptions(_environmentList);
                    EnvironmentDropdown.RefreshShownValue();
                    Debug.Log($"Dropdown atualizado com {_environmentList.Count} ambientes.");
                }
            }
            else
            {
                Debug.Log("Nenhum ambiente encontrado.");
            }
        }

        public void SelectEnvironment()
        {
            _selectedEnvironment = EnvironmentDropdown.options[EnvironmentDropdown.value].text;
            _hasEnvironment = true;
            SelectEnvironmentPanel.SetActive(false);
        }

        private void DoReturnToHomePage()
        {
            Controller.SwitchToHomePage();
        }

        private void DoHideInstructionBar()
        {
            InstructionBar.SetActive(false);
        }

        public void HideSetEnviromnent()
        {
            EnvironmentPanel.SetActive(false);
            SelectEnvironmentPanel.SetActive(true);
        }

        public void HideDeleteNote()
        {
            DeletePanel.SetActive(false);
        }

        private void SetSaveButtonActive(bool active)
        {
            SaveButton.enabled = active;
            SaveButton.GetComponentInChildren<Text>().color = active ? _activeColor : Color.gray;
        }

        public void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteAll();

            PlayerPrefs.Save();

            Debug.Log("PlayerPrefs limpo com sucesso!");
        }
    }
}
