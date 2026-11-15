# 공용 오디오 작업 정리와 PR 인계

기준일: 2026-11-13
상태: 구현 완료·최종 검증 대기. 현재 작업을 코드·에셋·문서와 함께 커밋해 보존하며 PR 생성·push·main 병합은 이번 요청 범위가 아니다.

## 이번에 보존하는 범위

Unity 기본 AudioSource·AudioListener·AudioMixer를 사용한 공용 오디오 관리다. 전체·음악·효과음 설정, 로컬 미리듣기/적용/취소, 24개 음원 풀, 분류·큐·발생 주체별 재생 제한, 우선순위·거리 처리와 Scene 종료 정리를 구현했다. Main/Credits의 기존 음악 시간은 유지하며 Player·Cannon/Missile·BlackWolf의 일부 효과음에 연결했다. [API·컴포넌트·에셋 제작 규칙](../../../../02_architecture/01_core/game-audio-api.md)이 사용법의 기준이다.

## 검증 결과와 완료 판단

| 구분 | 결과 |
| --- | --- |
| Unity 컴파일 | 오류 0 / 경고 0 |
| 오디오 자동 검사 | 6/6 통과 |
| 기존 무기 UI 회귀검사 | 2/2 통과, 독립 임시 Scene |
| 실제 마우스 | Startup 설정 열기·음량 변경·취소·적용·재열기, 독립 Scene의 원본 Player 발사 확인 |
| 실제 오디오 출력 계측 | Main 대기 BGM, 효과음 좌우 방향·거리 감쇠, 산탄 한 발당 음원 1개 확인 |
| 정적 검사 | 전체 Unity `.meta` 검사 오류 0, 변경 문서 링크와 diff 검사 통과 |
| 실제 전투 Scene 전체 경로 | 미완료. 아래 기존 Scene 오류로 제한 |
| 원격 Client·Standalone | 미검증 |

따라서 **공용 AudioManager의 핵심 구현이 완료된 상태**이며 게임 전체 오디오의 최종 인수가 끝났다는 뜻은 아니다. 검사 원본과 실제 화면은 공유 제외 `Docs/Local/Validation/Audio20261113`에 보존했다. 수동 검수 후 Play를 종료하고 원래 Initial_Stage·설정 상태로 복원했다.

## 남은 확인과 콘텐츠 작업

1. Startup→Stage1의 `TurretMatchBinding.BindMatch`에서 발생한 `FieldCanonTurret LV1 (1)` authored entity ID 누락을 해결한 후 실제 전투 경로를 다시 확인한다. Initial_Stage의 `SpaceshipEnter.OnTriggerEnter2D`/`OnTriggerExit2D` NullReference도 별도 Scene 구성 문제로 기록한다. 이번 오디오 작업에서는 해당 Scene·ID·트리거를 수정하지 않았다.
2. 실제 Player·Enemy·Turret의 전투 중 출력, 거리·중첩·전환을 확인한다. 독립 Scene 검수로 전체 전투 Scene 인수를 대체하지 않는다.
3. 원격 Client에서 터렛 발사·폭발음 전달을 확인하고 필요한 기존 표현 경계의 연결 범위를 정한다. 이번에 새 원격 오디오 RPC는 추가하지 않았다.
4. 모든 무기의 개별 음색, 모든 Enemy, Laser 지속음, UI 클릭음은 콘텐츠 후속이다. 이 작업을 AudioManager 기반 미구현으로 혼동하거나 이미 전부 연결됐다고 기록하지 않는다. 기기별 최종 음량·Standalone 음향 인수도 남아 있다.

## 추후 PR과 Docs 반영

사용자 요청: **나중에 PR을 만들 때 Docs 반영도 함께 처리한다.** 현재는 로컬 게임 저장소의 Docs에 기록한 상태이며 별도 Docs 배포·원격 게시 완료를 뜻하지 않는다.

- PR base는 `main`으로 명시하고 실제 base/head를 다시 확인한다. 현재 작업 브랜치에 다른 파트 변경이 함께 있으므로 오디오 커밋과 의존 범위를 먼저 확인한다.
- 게임 PR의 문서 변경에는 [오디오 API](../../../../02_architecture/01_core/game-audio-api.md), [Core 구현 현황](../../../../02_architecture/01_core/core-implementation-status.md), [공통 UI 규칙](../../../../94_art/common-ui-rules.md), [OST 출력 연결](../../../../94_art/frontend-soundtrack.md), Docs/아키텍처 목차와 이 기록을 포함한다. 별도 Docs 반영 절차가 있으면 함께 확인하며 잊지 않는다.
- PR 설명은 저장소 템플릿을 사용하고 구현 완료와 최종 검증 대기를 구분한다. 실제 Scene 오류·원격 Client·콘텐츠 후속을 제외 범위로 명시하며 전체 완료나 Issue 종료를 추정하지 않는다.
- 먼저 볼 파일: `GameAudioManager.cs`의 재생/풀/수명, `GameAudioSettings.cs`와 `SettingsScreenPresenter.cs`의 설정 트랜잭션, `AppRoot.cs`의 초기화/종료, `GameAudioSetup.cs`와 Configuration 에셋의 제작 참조. 이어서 Player/Turret/Enemy 호출부와 Main/Credits 출력 연결을 확인한다.
- 코드 흐름은 `AppRoot → GameAudioConfiguration/Settings → GameAudioManager → 기본 AudioSource/AudioMixer`, Feature의 실제 발사·상태 변경 → 큐 재생 요청, 설정 슬라이더 → 미리듣기 → 적용 저장 또는 취소 복원 순서로 설명한다.
- 공유 제외 검수 이미지는 PR을 만들 때 실제 첨부 가능 여부를 확인한다. 로컬 경로만 적고 원격에서 열리는 증거라고 설명하지 않는다.

기존 다른 파트의 미커밋 변경·스테이징 파일은 이 오디오 커밋에 섞지 않는다. 이 기록은 새로운 승인 인원이나 팀 운영 정책을 추가하지 않는다.
