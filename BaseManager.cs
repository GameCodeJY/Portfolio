/*
 * Coder        :   JY
 * Last Update  :   2026. 04. 13.
 * Information  :   매니저 기본 클래스 (서비스 로케이터 기능 탑재)
 */

namespace MainSystem
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// 씬 하이어라키에 배치되는 Hub 오브젝트.
    /// Signup / Get / Unsignup 으로 컴포넌트 간 직접 참조를 제거합니다.
    /// DontDestroyOnLoad 싱글톤과 달리 씬과 함께 소멸하므로
    /// 참조 수명이 씬 생명주기와 자동으로 일치합니다.
    /// </summary>
    public class ServiceLocator : MonoBehaviour
    {
        #region Data Field
        // 외부에서 직접 접근하지 않도록 private으로 제한합니다.
        private readonly Dictionary<Type, object> _registry = new();
        #endregion

        #region Initialization
        /// <summary> 서브클래스에서 오버라이드해 사전 할당(new)을 수행합니다. </summary>
        protected virtual void Allocate() { }

        /// <summary> 외부에서 호출하는 초기화 진입점입니다. </summary>
        public virtual void Initialize() => Allocate();
        #endregion

        #region Service Locator
        /// <summary>
        /// 단일 서비스를 타입 키로 등록합니다.
        /// 같은 타입이 이미 등록된 경우 경고를 출력하고 무시합니다.
        /// </summary>
        public void Signup<T>(T service) where T : class
        {
            Type key = typeof(T);
            if (_registry.TryAdd(key, service) == false)
                Debug.LogWarning($"[{GetType().Name}] {key.Name} 이(가) 이미 등록되어 있습니다.");
        }

        /// <summary>
        /// 인스펙터에서 할당한 MonoBehaviour 리스트를 일괄 등록합니다.
        /// 씬마다 서비스 조합을 코드 수정 없이 교체할 수 있습니다.
        /// </summary>
        protected void RegisterServicesFromList(IEnumerable<MonoBehaviour> services)
        {
            if (services == null) return;

            foreach (var service in services)
            {
                if (service == null) continue;

                Type key = service.GetType();
                if (_registry.TryAdd(key, service) == false)
                    Debug.LogWarning($"[{GetType().Name}] {key.Name} 이(가) 이미 등록되어 있습니다. (List 등록 중)");
            }
        }

        /// <summary>
        /// 서비스를 레지스트리에서 제거합니다.
        /// Hub 소멸 시 참조가 자동으로 해제됩니다.
        /// </summary>
        public void Unsignup<T>() where T : class => _registry.Remove(typeof(T));

        /// <summary>
        /// 등록된 서비스를 반환합니다.
        /// 자식 매니저에서만 호출하도록 protected로 제한해
        /// 외부 직접 참조를 컴파일 타임에 차단합니다.
        /// </summary>
        protected T Get<T>() where T : class
        {
            if (_registry.TryGetValue(typeof(T), out object service))
                return service as T;

            Debug.LogWarning($"[{GetType().Name}] {typeof(T).Name} 을(를) 찾을 수 없습니다.");
            return null;
        }
        #endregion
    }
}
