/*
 * Coder       :   JY
 * Last Update :   2026. 04. 13.
 * Information :   Scene Manager — Lazy Activation + Additive Scene 전략
 */

namespace MainSystem
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Lazy Activation + Additive Scene 전략을 적용한 씬 매니저입니다.
    /// 앱 시작 시 allowSceneActivation = false 로 전 씬을 메모리에 먼저 로딩하고,
    /// 전환 시 SetActive 만으로 즉시 표시해 플레이 중 로딩 화면을 완전히 제거합니다.
    /// 씬을 언로드하지 않고 루트 오브젝트만 숨기므로 뒤로 가기 전환도 즉시 처리합니다.
    /// /// </summary>
    public class SceneManager : GenericSingleton<SceneManager>
    {
        #region Member Values
        private GlobalUIManager _uiManager;
        private LoadingScreen   _loadingScreen;
        private BaseScene       _activeScene;

        private AsyncOperation _asyncOperation;
        private bool           _isSceneLoading = false;
        private bool           _isSwitching    = false;

        private readonly ThreadPriority _threadPriority = ThreadPriority.Normal;

        // Additive 씬 이름 목록
        private readonly List<string> _additiveLoadedScenes = new();

        // 씬별 대기 중인 AsyncOperation (allowSceneActivation = false 상태)
        private readonly Dictionary<string, AsyncOperation> _pendingOps  = new();

        // 씬별 루트 오브젝트 캐시 — 재활성화 시 SetActive(true) 대상
        private readonly Dictionary<string, List<GameObject>> _activeRoots = new();

        private string _nowActiveSceneName;
        private string _activedSceneName;
        #endregion

        #region Property
        public bool   IsShowStoryLoadingScreen { get; set; } = false;
        public string ActivedSceneName => _activedSceneName;
        #endregion

        #region Initialization
        protected override void Initialize()
        {
            DontDestroyOnLoad(this);
            _uiManager = GlobalUIManager.Instance;
            SaveActivatedSceneName();
        }

        public void SignupActiveScene(BaseScene baseScene) => _activeScene = baseScene;
        #endregion

        #region Standard / Async Loading
        public void SceneLoadStandard(string sceneName)
        {
            SaveActivatedSceneName();
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        public void SceneLoadAsync(string sceneName)
        {
            if (_isSceneLoading) return;
            _isSceneLoading = true;

            SaveActivatedSceneName();
            LoadingScreenSetup();
            StartCoroutine(SceneLoadingAsyncRoutine(sceneName));
        }
        #endregion

        #region Additive — Preload & Switch
        /// <summary>
        /// 앱 시작 시 전 스테이지 씬을 백그라운드 로딩합니다 (progress 0.9 까지).
        /// allowSceneActivation = false 로 Awake 를 차단한 채 메모리만 할당합니다.
        /// </summary>
        public void PreloadAllScenes(Action<float> onProgress, Action onComplete)
            => StartCoroutine(PreloadAllStagesRoutine(onProgress, onComplete));

        public void LoadSceneAdditiveAsync(string sceneName)
        {
            if (_additiveLoadedScenes.Contains(sceneName) ||
                UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                Debug.LogWarning($"[SceneManager] {sceneName} 씬은 이미 로드되어 있습니다.");
                return;
            }
            StartCoroutine(LoadAndCacheSceneRoutine(sceneName));
        }

        /// <summary>
        /// Additive 씬으로 즉시 전환합니다.
        /// 프리로드가 없는 씬은 일반 Async 로딩으로 폴백합니다.
        /// </summary>
        public void SwitchToScene(string targetScene, bool unloadPrevious = false)
        {
            if (!_additiveLoadedScenes.Contains(targetScene))
            {
                SceneLoadAsync(targetScene);
                return;
            }
            StartCoroutine(SwitchToSceneCoroutine(targetScene, unloadPrevious));
        }

        public void UnloadSceneAdditive(string sceneName)
        {
            if (!_additiveLoadedScenes.Contains(sceneName))
            {
                Debug.LogWarning($"[SceneManager] {sceneName} 씬은 로드되어 있지 않습니다.");
                return;
            }
            UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(sceneName);
            _additiveLoadedScenes.Remove(sceneName);
        }
        #endregion

        #region Coroutines
        private IEnumerator SwitchToSceneCoroutine(string targetScene, bool unloadPrevious)
        {
            if (_isSwitching) yield break;
            _isSwitching = true;

            // 1) 현재 씬 루트 오브젝트 일괄 비활성화 (언로드 없이 숨김)
            if (!string.IsNullOrEmpty(_nowActiveSceneName))
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(_nowActiveSceneName);
                var roots = new List<GameObject>();
                scene.GetRootGameObjects(roots);
                foreach (var go in roots) if (go) go.SetActive(false);

                if (unloadPrevious)
                {
                    yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(_nowActiveSceneName);
                    _activeRoots.Remove(_nowActiveSceneName);
                }
            }

            // 2) allowSceneActivation = true — 이미 메모리에 있으므로 지연 없음
            if (_pendingOps.TryGetValue(targetScene, out var op) && !op.isDone)
            {
                op.allowSceneActivation = true;
                while (!op.isDone) yield return null; // Awake / OnEnable 발생 시점
                _pendingOps.Remove(targetScene);
            }
            else if (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(targetScene).isLoaded)
            {
                var op2 = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Additive);
                while (!op2.isDone) yield return null;
            }

            // 3) 루트 캐시 생성 (처음 표시하는 씬)
            if (!_activeRoots.ContainsKey(targetScene))
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(targetScene);
                var roots = new List<GameObject>();
                scene.GetRootGameObjects(roots);

                var actives = new List<GameObject>();
                foreach (var go in roots)
                    if (go && go.activeSelf) actives.Add(go);

                _activeRoots[targetScene] = actives;
            }

            // 4) 루트 오브젝트 복원 + ActiveScene 지정
            foreach (var go in _activeRoots[targetScene]) if (go) go.SetActive(true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(
                UnityEngine.SceneManagement.SceneManager.GetSceneByName(targetScene));
            _nowActiveSceneName = targetScene;

            _isSwitching = false;
        }

        private IEnumerator PreloadAllStagesRoutine(Action<float> onProgress, Action onComplete)
        {
            int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;

            for (int i = 2; i < sceneCount; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string name = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(name) &&
                    !_additiveLoadedScenes.Contains(name) &&
                    !UnityEngine.SceneManagement.SceneManager.GetSceneByName(name).isLoaded)
                {
                    _additiveLoadedScenes.Add(name);
                }
            }

            int total = _additiveLoadedScenes.Count;
            if (total == 0) { onProgress?.Invoke(1f); onComplete?.Invoke(); yield break; }

            int done = 0;

            foreach (var sceneName in _additiveLoadedScenes)
            {
                if (_pendingOps.ContainsKey(sceneName) ||
                    UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName).isLoaded)
                {
                    done++;
                    continue;
                }

                var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                op.allowSceneActivation = false; // Awake 차단, 메모리만 할당
                _pendingOps[sceneName] = op;

                while (op.progress < 0.9f)
                {
                    onProgress?.Invoke((done + op.progress) / total);
                    yield return null;
                }

                done++;
                onProgress?.Invoke((float)done / total);
            }

            onComplete?.Invoke();
        }

        private IEnumerator LoadAndCacheSceneRoutine(string sceneName)
        {
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) yield break;

            _additiveLoadedScenes.Add(sceneName);

            var roots   = new List<GameObject>();
            var actives = new List<GameObject>();
            scene.GetRootGameObjects(roots);

            foreach (var go in roots)
            {
                if (go.activeSelf) actives.Add(go);
                go.SetActive(false);
            }
            _activeRoots[sceneName] = actives;
        }

        private IEnumerator SceneLoadingAsyncRoutine(string sceneName)
        {
            StartOperation(sceneName);
            while (_asyncOperation.progress < 0.9f)
            {
                _loadingScreen?.RefreshLoadingProgressBar(_asyncOperation.progress);
                yield return null;
            }
            _loadingScreen?.FinishSceneLoading();
            yield return null;
        }
        #endregion

        #region Private Helpers
        private void StartOperation(string sceneName)
        {
            Application.backgroundLoadingPriority = _threadPriority;
            _asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName);
            _asyncOperation.allowSceneActivation = false;
        }

        public void SceneLoadingDone()
        {
            _isSceneLoading = false;
            if (_asyncOperation != null) _asyncOperation.allowSceneActivation = true;
        }

        private void LoadingScreenSetup() => _loadingScreen = _activeScene.SetActiveLoadingScreen();

        private void SaveActivatedSceneName()
            => _activedSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        #endregion
    }
}
