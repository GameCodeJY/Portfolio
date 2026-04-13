/*
 *  Coder       :   JY
 *  Last Update :   2026. 04. 13.
 *  Information :   스탯 모디파이어
 */

namespace MainSystem
{
    using GoogleSheet.Core.Type;
    using System.Collections.Generic;
    using UnityEngine;

    // ==== Modifier primitives ==================================================

    [UGS(typeof(ModKind))]
    public enum ModKind { Flat, PercentAdd, PercentMult }

    public readonly struct StatModifier
    {
        public readonly ModKind Kind;
        public readonly float   Value;
        public readonly float   ExpireAt;
        public readonly int     Order;

        public StatModifier(ModKind kind, float value, float expireAt, int order = 0)
        {
            Kind     = kind;
            Value    = value;
            ExpireAt = expireAt;
            Order    = order;
        }

        /// <summary> 만료 시각이 설정되어 있고 현재 시각이 그 이상이면 만료로 판정합니다. </summary>
        public bool IsExpired(float now) => ExpireAt > 0f && now >= ExpireAt;
    }

    // ==== Per-stat calculator ==================================================

    /// <summary>
    /// Dirty Flag 패턴을 적용한 스탯 계산기입니다.
    /// Modifier 변화 시에만 _dirty = true 로 마킹하고,
    /// Value 접근 시 1회만 재계산 후 캐시해 매 프레임 O(n) 순회를 제거합니다.
    /// string Key 를 버프 ID 로 사용해 동일 키 재삽입 시 자동 갱신되므로
    /// 중복 버프 적용이 원천 차단됩니다.
    /// </summary>
    public sealed class StatWithMods
    {
        private float _baseValue;

        // Key: 버프 식별자(string) — 재삽입 시 자동 갱신(중복 차단)
        private readonly Dictionary<string, StatModifier> _mods = new(8);

        private float _cachedValue;
        private bool  _dirty = true;

        // CullExpired 에서 순회 중 Remove 를 피하기 위한 Pre-allocated 버퍼
        private readonly List<string> _tempRemoveKeys = new(4);

        public StatWithMods(float baseValue) => _baseValue = baseValue;

        public float BaseValue
        {
            get => _baseValue;
            set { _baseValue = value; _dirty = true; }
        }

        /// <summary>
        /// Modifier 가 변경된 경우에만 재계산합니다 (Dirty Flag).
        /// 적용 순서: Flat → PercentAdd → PercentMult
        /// </summary>
        public float Value
        {
            get
            {
                if (!_dirty) return _cachedValue;

                float flat = 0f, addPct = 0f, mult = 1f;

                foreach (var m in _mods.Values)
                {
                    switch (m.Kind)
                    {
                        case ModKind.Flat:        flat   += m.Value;          break;
                        case ModKind.PercentAdd:  addPct += m.Value;          break;
                        case ModKind.PercentMult: mult   *= 1f + m.Value;     break;
                    }
                }

                _cachedValue = (_baseValue + flat) * (1f + addPct) * mult;
                _dirty = false;
                return _cachedValue;
            }
        }

        /// <summary>
        /// 한시적 Modifier 를 추가합니다.
        /// durationSec &lt;= 0 이면 영구 적용합니다.
        /// </summary>
        public void AddTemp(string key, ModKind kind, float value, float durationSec, int order = 0)
        {
            float expire = durationSec > 0f ? Time.time + durationSec : 0f;
            _mods[key]   = new StatModifier(kind, value, expire, order);
            _dirty       = true;
        }

        /// <summary> 지정 키의 Modifier 를 제거합니다. </summary>
        public void Remove(string key)
        {
            if (_mods.Remove(key))
                _dirty = true;
        }

        /// <summary>
        /// 만료된 Modifier 를 일괄 제거합니다.
        /// 2-Pass 방식으로 순회 중 Remove 오류를 방지하고
        /// Pre-allocated 버퍼로 GC 할당을 제거합니다.
        /// </summary>
        public void CullExpired(float now)
        {
            _tempRemoveKeys.Clear();

            // Pass 1: 만료된 키 수집
            foreach (var kvp in _mods)
            {
                if (kvp.Value.IsExpired(now))
                    _tempRemoveKeys.Add(kvp.Key);
            }

            if (_tempRemoveKeys.Count == 0) return;

            // Pass 2: 일괄 삭제
            for (int i = 0; i < _tempRemoveKeys.Count; i++)
                _mods.Remove(_tempRemoveKeys[i]);

            _dirty = true;
        }
    }
}
