/*
 *  Coder       :   JY
 *  Last Update :   2026. 04. 13.
 *  Information :   Base Monster
 */

namespace MainSystem
{
    using System;
    using System.Collections;
    using UnityEngine;

    /// <summary>
    /// Object Pool + Staggered Update 전략을 적용한 몬스터 기본 클래스입니다.
    /// IPoolable 단일 진입점(ActiveObject → Revive)으로 풀 활성화 경로를 통일하고,
    /// BT Manual Tick 주기를 인스턴스마다 랜덤으로 분산해
    /// 동일 프레임에 AI 갱신이 집중되는 CPU 스파이크를 제거합니다.
    /// Die() 시 모든 Trigger 를 리셋해 풀 반환 후 Revive 시 Animator 상태 오염을 방지합니다.
    /// </summary>
    public class Monster : Character, IPoolable
    {
        #region Reference Cache
        protected Animator          _animator            => MonsterComponentHub.Animator;
        protected Collider          _collider            => MonsterComponentHub.Collider;
        protected HPComponent       _health              => MonsterComponentHub.Health;
        protected StatController    _monsterStatController => MonsterComponentHub.StatController;
        protected StatusController  _statusController    => MonsterComponentHub.StatusController;
        protected ImmunityController _immunityController => MonsterComponentHub.ImmunityController;
        protected MoveHandler       _moveHandler         => MonsterComponentHub.MoveHandler;
        #endregion

        #region Member Values
        [SerializeField] protected string _monsterIndex;

        [SerializeField] private float _minTickDelay;
        [SerializeField] private float _maxTickDelay;

        private MonsterComponentHub _monsterHub;
        private int             _deathId;
        private WaitForSeconds  _deathWait;
        private Coroutine       _tickLogic;
        private WaitForSeconds  _tickDelay;
        #endregion

        #region Property
        public MonsterComponentHub MonsterComponentHub
        {
            get
            {
                _monsterHub ??= _hub as MonsterComponentHub;
                return _monsterHub;
            }
        }
        #endregion

        #region Events
        public Func<Transform, string, Monster> SpawnMonster;
        public event Action<Monster> DeathStartEvent;
        public event Action<Monster> DeathEndEvent;
        #endregion

        #region Initialization & Lifecycle
        public virtual void Initialize(string monsterIndex)
        {
            if (string.IsNullOrEmpty(monsterIndex)) return;

            _monsterIndex = monsterIndex;
            _deathId      = Animator.StringToHash("Death");
            _deathWait    = new WaitForSeconds(1f);

            base.Initialize();
        }

        protected override void InitializeStatController()
            => _monsterStatController.Initialize(_monsterIndex);

        /// <summary>
        /// IPoolable 단일 진입점 — Pool.Get() → ActiveObject() → Revive() 경로를 통일합니다.
        /// </summary>
        public virtual void ActiveObject() => Revive();

        public override void Revive()
        {
            base.Revive();

            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localPosition = Vector3.zero;

            MonsterComponentHub.Model.gameObject.SetActive(true);
            _health.FillToMax();
            if (_collider != null) _collider.enabled = true;

            _animator.Play("Idle", 0, 0f);
            MonsterComponentHub.BehaviorTree.enabled = true;

            // Staggered Update — 인스턴스마다 Tick 주기를 랜덤으로 분산합니다.
            if (MonsterComponentHub.BehaviorTree.UpdateMode == Opsive.BehaviorDesigner.Runtime.Components.UpdateMode.Manual)
            {
                _tickDelay = new WaitForSeconds(UnityEngine.Random.Range(_minTickDelay, _maxTickDelay));
                _tickLogic = StartCoroutine(TickLogic());
            }

            MonsterComponentHub.BehaviorTree.RestartBehavior();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            DeathStartEvent = null;
            DeathEndEvent   = null;
            SpawnMonster    = null;

            // _tickLogic 참조로 정지합니다.
            if (_tickLogic != null)
            {
                StopCoroutine(_tickLogic);
                _tickLogic = null;
            }
        }

        private IEnumerator TickLogic()
        {
            while (true)
            {
                MonsterComponentHub.BehaviorTree.Tick();
                yield return _tickDelay;
            }
        }
        #endregion

        #region TakeDamage
        protected override void TakeDamage(AttackContext attackInfo)
        {
            _hub.StoreEffectTirgger.TriggerEffect(EEffectName.Hit, EEffectTrigger.Play);

            if (_immunityController.IsImmunity(EStatus.STUN)) return;

            _hub.BuffController.ApplyBuff(attackInfo.ListBuff);
            _statusController.AddStatus(attackInfo.ListStatus, attackInfo.Excuter);
        }
        #endregion

        #region Death
        protected override void Die()
        {
            base.Die();

            // 모든 Trigger 리셋 — 풀 반환 후 Revive 시 Animator 상태 오염을 방지합니다.
            foreach (var param in _animator.parameters)
            {
                if (param.type == AnimatorControllerParameterType.Trigger)
                    _animator.ResetTrigger(param.name);
            }

            _collider.enabled = false;
            _moveHandler.SetMoveState(false);
            _hub.StoreEffectTirgger.TriggerEffect(EEffectName.Death, EEffectTrigger.Play);

            DeathStartEvent?.Invoke(this);

            if (_animator.HasState(0, _deathId))
                _animator.Play(_deathId);
            else
                StartCoroutine(DelayDeathEnd());
        }

        private IEnumerator DelayDeathEnd()
        {
            _animator.Play("Idle");
            _animator.Update(0f);
            MonsterComponentHub.Model.gameObject.SetActive(false);

            yield return _deathWait;

            DeathEnd();
        }

        public void DeathStart() => DeathStartEvent?.Invoke(this);

        public virtual void DeathEnd()
        {
            _animator.Play("Idle");
            _animator.Update(0f);

            DeathEndEvent?.Invoke(this);
            MonsterComponentHub.BehaviorTree.enabled = false;
        }
        #endregion
    }
}
