# Unity Developer Portfolio

> **이준용 · Game Client Developer**  
> 3D 액션 로그라이크 DarkRebirth (스마트소프트, 2024.09 – 2026.03) 개발 중 작성한 코드 샘플입니다.  
> 각 샘플은 **적용 패턴 · 선택 이유 · Trade-off** 순으로 구성했습니다.

---

## Samples

### 01. BaseManager.cs — Service Locator `[IoC]` `[Dependency Inversion]`

**패턴** Service Locator (Hub)

**적용**  
Hub를 씬 하이어라키에 배치하고, `Signup<T>()`으로 등록 · `Get<T>()`으로 조회하는 방식으로 컴포넌트 간 직접 참조를 제거합니다.

**선택 이유**
- Hub가 씬과 함께 소멸하므로, 참조 수명이 씬 생명주기와 자동으로 일치 → null 역참조 근절
- `Get<T>()`를 `protected`로 제한해 외부 직접 참조를 컴파일 타임에 차단
- 인스펙터 리스트에 서비스를 등록해 씬마다 서비스 조합을 코드 수정 없이 교체 가능

**Trade-off**  
완전한 DI 프레임워크(Zenject) 대비 기능 제한 — 외부 패키지 없이 Unity 생명주기에 자연스럽게 통합되는 실용성으로 채택

---

### 02. StatWithMods.cs — Stat System `[Dirty Flag]` `[Game Programming Patterns]`

**패턴** Dirty Flag + Composite Modifier

**적용**  
Modifier 변화 시에만 `_dirty = true`로 마킹하고, `Value` 접근 시 1회 재계산 후 캐시합니다. `string` Key로 모디파이어를 식별·관리합니다.

**선택 이유**
- 스탯 재계산을 Modifier 변화 시점으로 한정해 매 프레임 O(n) 순회를 제거
- `string` Key를 버프 ID로 사용해, 동일 키 재삽입 시 자동 갱신 → 중복 버프 적용 원천 차단
- `RemoveModifierFromAll(key)`으로 하나의 Key에 묶인 복수 스탯 버프를 일괄 해제 → 해제 누락 방지
- 2-Pass `CullExpired`로 순회 중 Remove 오류를 방지하고, Pre-allocated Buffer로 GC 할당 제거

**Trade-off**  
Dictionary는 삽입 순서 미보장 — `Order` 필드로 Flat → PercentAdd → PercentMult 적용 순서를 별도 제어

---

### 03. BasicAttackState.cs — Player FSM `[GoF State]` `[Input Buffering]` `[Event-Driven]`

**패턴** State 패턴 + Input Buffering

**적용**  
전투 상태 10종을 `StateMachineBehaviour` 독립 클래스로 분리하고, `normalizedTime` 기반 공격 판정 · EventBus 발송 · 콤보 선입력을 구현합니다.

**선택 이유**
- 상태별 클래스를 분리해, 새 상태 추가 시 기존 코드 수정 없이 확장 가능 (OCP 준수)
- `normalizedTime` 기반 판정으로 AnimationSpeed 변화에 자동 대응해 히트박스 타이밍 일관성을 보장
- EventBus `Broadcast`를 통해 State와 피격·이펙트 로직 간 직접 의존을 완전히 제거
- Input Buffering으로 콤보 응답성을 높이고, `ActionFlag`로 스킬 사용 중 콤보 진입을 차단

**Trade-off**  
`StateMachineBehaviour`는 Animator 종속 — 보스처럼 복잡한 전이가 필요한 경우 코드 기반 BehaviorTree(Opsive)를 별도 채택

---

### 04. SceneManager.cs — Scene Manager `[Lazy Activation]` `[Additive Scene]`

**패턴** Lazy Activation + Additive 씬 프리로드

**적용**  
앱 시작 시 `allowSceneActivation = false`로 전 씬을 선로딩해두고, 전환 시 `SetActive`만으로 즉시 표시합니다.

**선택 이유**
- 씬 로딩 비용을 앱 초기화 시점으로 모두 이전해, 플레이 중 로딩 화면을 완전히 제거
- 씬을 언로드하지 않고 루트 오브젝트만 숨겨, 뒤로 가기 전환도 즉시 처리
- `allowSceneActivation = true` 전환 시 이미 메모리에 올라온 씬을 Awake 재실행 없이 즉시 활성화

**Trade-off**  
전 씬 메모리 유지 → RAM 예산 압박 — `unloadPrevious` 플래그로 메모리 예산에 따라 즉시 언로드 vs. 유지 선택 가능

---

### 05. Monster.cs — Monster `[Object Pool]` `[Staggered Update]`

**패턴** Object Pool + Staggered Update

**적용**  
Pool 재사용으로 Instantiate·Destroy를 제거하고, BT Manual Tick 주기를 인스턴스마다 `Random.Range`로 분산해 CPU 부하를 균등화합니다.

**선택 이유**
- `IPoolable` 단일 진입점으로 `Pool.Get()` → `ActiveObject()` → `Revive()` 활성화 경로를 통일
- 인스턴스마다 BT Tick 주기를 랜덤으로 분산해 동일 프레임에 AI 갱신이 집중되는 CPU 스파이크를 제거
- `Die()` 시 모든 Trigger를 리셋해 풀 반환 후 Revive 시 Animator 상태 오염을 완전히 차단

**Trade-off**  
Staggered Update 최대 `_maxTick` 지연 허용 — 일반 몬스터 0.1~0.3s 범위 / 보스는 반응 속도가 중요해 Manual Tick 미적용

---

## Tech Stack

| 항목 | 내용 |
|---|---|
| Engine | Unity (C#) |
| Rendering | URP · HLSL Shader · VFX Graph |
| AI | Opsive Behavior Designer |
| Data | Google Sheet (UGS) · SQLite |
| Tools | Git / GitHub · Confluence |
| Platform | PC · Android · iOS |

---

## Contact

- Email · ljy41775@gmail.com  
- GitHub · [github.com/GameCodeJY](https://github.com/GameCodeJY)
