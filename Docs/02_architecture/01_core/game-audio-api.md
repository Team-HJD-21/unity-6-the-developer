# Unity 기본 오디오와 GameAudioManager

기준일: 2026-11-13. 상태: 구현 완료·최종 검증 대기, main 미반영. [이번 작업 정리와 추후 PR의 Docs 반영 사항](../../01_planning/02_implementation-records/2026/11/2026-11-13-game-audio-handoff.md)을 함께 읽는다.

추가 오디오 패키지나 자체 공간 음향 엔진 없이 Unity의 AudioSource, AudioListener, AudioMixer를 사용한다. 공용 코드는 재생 요청·풀·동시 재생 수·분류·수명과 로컬 설정을 관리한다.

## 3D 소리에 필요한 컴포넌트

Unity 기본 구성은 소리가 나는 쪽의 **AudioSource**와 듣는 쪽의 **AudioListener**다. AudioSource의 Spatial Blend를 1(3D)로 두면 Unity가 방향과 거리 감쇠를 계산한다. 별도 package는 필요 없다. 현재 CameraManager가 선택한 출력 카메라의 Listener 하나를 사용하므로 Enemy나 Turret에 Listener를 더 붙이지 않는다.

이번 공용 효과음 경로는 `Assets/Prefabs/Audio/AudioVoice.prefab`의 **표준 AudioSource**를 풀에서 꺼내 쓴다. 따라서 연결된 Player·Turret에 AudioSource를 각각 추가할 필요가 없다. Enemy의 기존 `EnemyAnimation.AbilitySound`에 큐를 할당하면 기존 능력 애니메이션 호출에서 재생한다. 현재 음원을 할당한 Enemy는 BlackWolf다.

2D 탑다운 화면에서는 음원 좌표의 Z만 Listener 평면에 맞춘다. 실제 Player/Enemy/Projectile Transform은 바꾸지 않는다. 기본 Rolloff는 Linear, Doppler는 0이며 XY 거리는 유지한다. 카메라의 촬영 높이 때문에 가까운 총소리가 멀게 계산되는 문제를 피한다. 반사·벽 차폐·HRTF 처리는 이번 범위에 없다.

장식용 고정 환경음에 직접 AudioSource를 붙이는 것도 가능하다. 이때 Output을 GameAudio 믹서의 해당 그룹에 연결하고 Spatial Blend·Min/Max Distance를 설정한다. 직접 재생한 Source는 공용 풀의 동시 재생 제한에 포함되지 않는다. 반복 환경음도 공용 제한과 수명이 필요하면 아래 큐 API를 사용한다.

## 에셋과 편집 위치

| 에셋 | 편집 내용 |
| --- | --- |
| `Assets/Audio/Mixers/GameAudio.mixer` | Master 아래 Music / Effects, Effects 아래 UI / Player / Enemy / Turret / Ambience / Cinematic |
| `Assets/Audio/Configuration/*.asset`의 SoundClassProfile | 출력 그룹, 2D/3D, 우선순위, 분류별·발생 주체별 동시 재생 수, 재요청 간격, 거리 |
| 같은 폴더의 GameAudioCue | 원본 clip 후보, gain, pitch 범위, 반복 여부, 시작·재생 구간, 큐 동시 재생 수 |
| `Assets/Resources/Core/GameAudioConfiguration.asset` | 믹서·Music/Cinematic 출력·Voice Prefab·GameSound 목록, 풀 크기와 UI 예약 수 |
| `Assets/Prefabs/UI/Common/SettingsScreen.prefab` | 전체·음악·효과음 슬라이더와 기존 HUD 크기 설정 |

프로필은 UE Sound Class와 유사한 분류 용도이고, 그룹 gain과 DSP 출력은 Unity AudioMixer가 담당한다. 프로필/큐 제한은 이 공용 재생 경로에만 적용된다.

기본 풀은 24개, UI 예약은 2개다. 분류 상한은 Player 6 / Turret 8 / Enemy 6 / UI 3 / Ambience 3 / Cinematic 8이며 전체 풀 상한이 우선한다. 같은 발생 주체의 재요청은 짧게 억제하고, 계속 사격하면 오래된 발사음의 꼬리를 교체한다. 포화 시 낮은 중요도·먼 소리를 우선 교체하거나 새 요청을 버린다. Priority는 Unity 규칙과 같이 작은 숫자가 중요하다. Music은 priority 0이며 별도 기존 AudioSource로 재생한다.

`Tools > TeamHJD > The Developer > Audio > Configure native audio`는 초기 제작 도구다. Edit Mode에서만 실행하며 dirty Scene을 덮어쓰지 않는다. 기존 프로필/큐의 수동 조정값은 보존하지만 Settings 레이아웃과 음악 출력 참조는 제작 기본값으로 적용하므로 일반 수치 편집에는 Inspector를 사용한다. 믹서 최초 생성 어댑터는 Unity Editor 내부 제작 API를 사용하며 Unity 업그레이드 때 확인한다. 게임 실행에는 내부 Editor API를 사용하지 않는다.

## Feature 연결

```csharp
using TeamHJD.Game.Presentation;

// 실제 발사 성공 뒤 한 번 요청한다. this는 소리를 발생시킨 Component다.
GameAudioManager.Play(GameSound.PlayerShot, this);

// 별도 큐를 Inspector로 할당한 Feature도 같은 풀을 사용한다.
GameAudioManager.VoiceHandle voice = GameAudioManager.Play(cue, this);
// 반복음을 종료할 때 보관한 핸들로 요청한다.
voice.Stop();
```

관리자가 없거나 재생이 제한되면 비활성 핸들을 반환하며 게임 판정은 바꾸지 않는다. 슬롯을 재사용해도 오래된 핸들이 새 소리를 정지시키지 않는다. 반복음은 발생 주체를 따라가고 비활성화·파괴 때 종료한다. Scene이 언로드되면 그 Scene의 효과음을 정리한다. `FieldSimulationTime`에 바인딩된 발생 주체의 정지는 재생에도 반영한다. 바인딩되지 않은 임의 Source나 모든 원격 정지 상황까지 보장하는 계약은 아니다.

다음 기존 경계에 연결했다. 네트워크 권한·게임 명령·피해 계산은 변경하지 않는다.

| 진입점 | 소리 |
| --- | --- |
| `PlayerAttack.TryFire`의 실제 탄환 생성 성공 | 한 발당 PlayerShot 한 번. 산탄 pellet 수만큼 중복하지 않음 |
| `FieldTurretBase.BindProjectileSimulation` | 실제 Cannon/Missile 탄환 생성 뒤 발사음 |
| `FieldTurretBase`의 활성 상태 변경 성공 | TurretOn / TurretOff |
| `Explode.Start` | 기존 폭발 오브젝트의 Explosion |
| `EnemyAnimation` / `EnemyNetworkAnimator` | 로컬 또는 기존 Attack RPC 수신 시 능력음. Host 수신 중복은 기존 분기 유지 |

새 큐는 `Create > The Developer > Audio > Sound cue`, 새 분류는 `Sound class`로 만든다. 프로젝트에 이미 알맞은 분류가 있으면 재사용한다. Inspector 참조로 직접 재생하는 큐에는 GameSound 카탈로그 추가가 필요 없다. enum으로 요청할 때만 고유 Id를 정해 Configuration.Cues에 등록한다.

## 음원 제작 규칙

- 원본 WAV와 대응하는 `.meta`를 함께 보존한다. 기존 Legacy 믹서와 그 gain은 변경하지 않았다.
- 발사음·공격음은 가능한 mono로 제작한다. 반복하지 않는 짧은 SFX는 메모리 재생에 맞추고, 긴 BGM은 기존 Streaming 설정을 유지한다.
- 처음/끝의 긴 무음이나 잔향은 먼저 확인한다. 원본을 덮어 자르는 대신 큐의 StartSeconds / DurationSeconds로 사용 구간을 지정할 수 있다. Duration 0은 끝까지다.
- 현재 PlayerBullet은 시작 .035초/길이 .32초, Fire는 .03초/.4초, WolfBite는 .38초/.95초를 사용한다. 임의 피치 변화는 기본 적용하지 않았다.
- 루프는 반복 경계가 제작된 전체 clip을 사용한다. Loop와 부분 재생 구간을 함께 지정한 큐는 재생을 거절한다.
- 음량은 원본과 큐 gain을 먼저 조정하고 분류 믹서로 합친다. 동시 재생 제한은 과도한 중첩을 줄이며 최종 음향 mastering이나 clipping 방지 limiter를 대신하지 않는다.
- 알맞은 UI 클릭 음원이 없어 이번에 임의 효과음을 붙이지 않았다. UI 분류와 예약 슬롯은 준비돼 있다.

## BGM과 설정

`AppRoot`가 앱 수명에 한 번 관리자를 만들고 종료 때 정리한다. Main/Credits 재생자는 기존 AudioSource를 유지하면서 `RouteMusic`으로 Music에 연결한다. Credits의 FlightAudio와 파트 효과음은 `RouteCinematic`으로 Effects/Cinematic에 연결한다. OST의 시작·전환·복귀 sample 위치와 컷신 시각은 변경하지 않는다. [기존 OST 재생 규칙](../../94_art/frontend-soundtrack.md)을 함께 읽는다.

설정은 전체 / 음악 / 효과음 0~100%다. 슬라이더를 조작하면 즉시 미리듣고 적용 때만 `CBC.Audio.Master`, `CBC.Audio.Music`, `CBC.Audio.Effects`에 저장한다. 취소·Escape·창 해제는 열기 전 값으로 돌아간다. 기본값은 모두 100%다. 0은 등록된 Source를 실제 mute하고 믹서를 -80dB로 설정한다. 음량을 바꿔도 음악 재생 위치를 다시 시작하지 않는다. 설정은 로컬 출력 값이며 Match 저장·네트워크 데이터와 별개다.

## 11/13 검증과 남은 범위

- Unity recompile 오류 0 / 경고 0. 오디오 EditMode 검사 6/6, 기존 PlayerWeaponHudTests 2/2 통과. 실제 실행 Scene 검사와 구분한다.
- 실제 Startup에서 마우스로 Settings 열기, Music 0 미리듣기 설정, 취소 복원, Master 49% 적용·재열기·저장 읽기 확인. 화면의 슬라이더·레이블 겹침 없음. 검수용 저장 음량은 원래 상태로 복원했다.
- 원본 Player Prefab을 독립 임시 Scene에 배치해 실제 마우스로 발사했다. 발사 1회에 산탄 5개, Player 그룹 음원 1개, Listener 출력 peak .25452를 확인했다. 제작 Scene 전체 동작 검증과 구분하며 임시 오브젝트는 Play 종료로 정리했다.
- Main 실제 Music 출력과 단일 Listener 확인. 같은 Wolf 효과음을 Listener 기준 좌/우 6, 우 18에서 재생해 AudioListener PCM peak를 계측했다. 좌 6은 L .23999 / R 0, 우 6은 L 0 / R .23999, 우 18은 L 0 / R .06000이었다. 이는 기본 3D 출력의 방향·거리 검증이며 모든 스피커의 청감 인수는 아니다.
- Startup→Stage1에서 터렛 entity ID 누락, Initial_Stage에서 기존 SpaceshipEnter 트리거 NullReference가 관측돼 전체 실제 전투 경로 검수는 완료하지 않았다. 이번 오디오 변경으로 해당 Scene/ID/트리거를 수정하지 않았다. 검증 원본은 공유 제외 `Docs/Local/Validation/Audio20261113`에 있다.
- 모든 무기의 개별 음색, 모든 Enemy, Laser 지속음, UI 클릭음, 원격 Client의 Turret 발사·폭발 전달, 여러 기기에서의 최종 음량과 Standalone 빌드는 후속 확인이다. Enemy 능력음은 기존 Attack RPC를 사용하지만 새 원격 오디오 RPC는 추가하지 않았다.
