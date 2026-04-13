/*
 *  Coder       :   JY
 *  Last Update :   2026. 04. 13.
 *  Information :   Base Player Basic Attack State
 */

namespace MainSystem
{
    using UnityEngine;
    using SkillSystem;

    /// <summary>
    /// GoF State 패턴 적용 — 플레이어 전투 상태 10종을 StateMachineBehaviour 독립 클래스로 분리합니다.
    /// normalizedTime 기반 공격 판정으로 AnimationSpeed 변화에 자동 대응하고,
    /// EventBus Broadcast 로 State 와 피격·이펙트 로직 간 직접 의존을 제거합니다.
    /// Input Buffering 으로 현재 애니메이션이 끝나기 전 다음 콤보를 예약합니다.
    /// </summary>
    public class BasicAttackState : StateMachineBehaviour
    {
        #region Data Field
        protected Player _mainPlayer;

        [SerializeField] protected EActionFlag _nextAttackFlag;
        [SerializeField] protected EBasicAttackCombo _combo;

        /// <summary> 공격 판정 타이밍 (normalizedTime 0 ~ 1) </summary>
        [SerializeField] protected float _attackTiming;

        /// <summary> 다음 콤보 애니메이션 트리거 이름 </summary>
        protected string _nextAttackAnimationTrigger;

        protected bool _isAttack;
        protected bool _isGetMouseButtonDown;

        protected EventHandler            _eventHandler;
        protected PlayerMoveHandler       _moveHandler;
        protected PlayerSkillController   _playerSkillController;
        #endregion

        #region Initialization
        /// <summary>
        /// 필요한 컴포넌트 참조를 캐시합니다.
        /// null 체크를 ??= 로 통일해 중복 GetComponent 호출을 방지합니다.
        /// </summary>
        protected void Initialize(Animator animator)
        {
            _mainPlayer            ??= animator.GetComponent<Player>();
            _playerSkillController ??= _mainPlayer.PlayerComponentHub.PlayerSkillController;
            _eventHandler          ??= _mainPlayer.PlayerComponentHub.EventHandler;
            _moveHandler           ??= _mainPlayer.PlayerComponentHub.PlayerMoveHandler;
        }
        #endregion

        #region StateMachineBehaviour
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            Initialize(animator);

            _isAttack = false;
            _isGetMouseButtonDown = false;

            // 입력 방향으로 즉시 회전
            _mainPlayer.transform.rotation = _moveHandler.Rotation;
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // normalizedTime 기반 판정 — AnimationSpeed 가 변해도 타이밍이 일정합니다.
            TryAttack(stateInfo.normalizedTime);

            if (!AnimatorUtility.IsCurrentState(animator, stateInfo.fullPathHash, layerIndex))
                return;

            // Input Buffering — 애니메이션이 끝나기 전 다음 콤보를 예약합니다.
            CheckNextBasicAttack(animator);
        }
        #endregion

        #region Attack
        /// <summary>
        /// 공격 타이밍 도달 시 EventBus 로 공격 이벤트를 발송합니다.
        /// EventBus Broadcast 로 State 와 피격·이펙트 로직 간 직접 의존을 제거합니다.
        /// </summary>
        protected virtual bool TryAttack(float normalizedTime)
        {
            if (normalizedTime < _attackTiming || _isAttack)
                return false;

            _isAttack = true;
            _eventHandler.Broadcast(new BagicAttackStartEventData { combo = _combo });
            return true;
        }

        /// <summary>
        /// 다음 콤보 입력을 체크합니다.
        /// ActionFlagController 로 스킬 사용 중 콤보 진입을 차단합니다.
        /// </summary>
        protected virtual bool CheckNextBasicAttack(Animator animator)
        {
            if (!_mainPlayer.PlayerComponentHub.ActionFlagController.CanPerformAction(_nextAttackFlag))
                return false;

            if (_isGetMouseButtonDown || !Input.GetMouseButtonDown(0))
                return false;

            _isGetMouseButtonDown = true;
            animator.SetTrigger(_nextAttackAnimationTrigger); // 다음 콤보 전이 예약
            return true;
        }
        #endregion
    }
}
