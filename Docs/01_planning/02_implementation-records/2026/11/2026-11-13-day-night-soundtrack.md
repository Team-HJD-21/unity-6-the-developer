# 낮·밤·우주선 OST 연결 기록

2026-11-13 준비 / 11-14 실제 적용. 사용자 승인 오디오 서브에이전트1명이 준비했고 부모가 기존 파일 해시를 확인해 통합·Unity 검증했다. 음악 구현과 전체 경로/청감 인수는 구분한다.

## 동작과 진입점

GameAudioManager의 앱 수명 안에 gameplay Music voice 두 개를 추가하고, AppRoot.Update → RenderGameplaySoundtrack → SetGameplayMusic → GameplayMusicPlayer로 실제 로컬 actor/presence와 확정 환경을 읽는다. 기존 Main Intro/Ambient 두 Source와 Credits OST Source, SFX 풀·설정·컷씬 시간표는 보존한다.

Day/Night는 각4곡의 독립 shuffle bag이다. 한 묶음에서 서로 다른 후보를 한 번씩 요청하고 가능한 경우 경계 연속 반복을 피한다. 곡 끝은 DSP sample 길이로 계산해 같은 시간대의 다음 곡을 예약한다. 300초를 넘는 곡도 실제 게임 phase가 바뀌면 중간에 전환한다. 기본0.8초의 짧은 fade 구간 외에는 다른 시간대의 곡을 남기지 않는다. 빠른 되돌림·Hub 왕복·취소·종료는 마지막 context와 현재 gain에서 처리한다. 별도5분 타이머나 음악용 네트워크 상태를 만들지 않는다.

자세한 선택/전환/입고/편집 규칙은 [공용 Audio API](../../../../02_architecture/01_core/game-audio-api.md#낮밤우주선-ost)를 따른다.

## 입고 원본

모두 PCM16/stereo/48kHz다. 합계446,071,242byte(425.4067MiB), 서로 다른SHA256 9개, 개별100MiB 미만이다. 기존 OriginalSoundtrack 폴더 정책을 따라 한 번만 복사하고 원본 WAV를 수정하지 않는다. 현재 effective WAV filter/diff/merge는 unspecified이며 WAV/LFS 규칙을 새로 만들지 않았다.

| 구분 | 파일 | 초 |
|---|---|---:|
| Day1 | Day1ForwardMotion.wav | 308.68 |
| Day2 | Day2SteelFrotier.wav | 309.56 |
| Day3 | Day3OrbitalExchange.wav | 228.40 |
| Day4 | Day4FrontierRelay.wav | 223.56 |
| Night1 | Night1AfterSundown.wav | 304.76 |
| Night2 | Night2IronVigil.wav | 207.84 |
| Night3 | Night3GhostCircuit.wav | 227.92 |
| Night4 | Night4DistanceLights.wav | 268.76 |
| Spaceship | SpaceshipAHomeBetweenStars.wav | 243.16 |

신규9곡만 Streaming/Vorbis0.9, 원본 sample rate/stereo 보존, preload=false/background=true로 입고한다. 기존 Main/Credits import와 파형은 보존한다. 라이브러리는 `Assets/Audio/Configuration/GameplayMusic.asset`, 연결은 `Resources/Core/GameAudioConfiguration.asset`이다.

## 실제 확인과 제한

- 원본9개 헤더·SHA256·sample peak/PCM RMS 조사 완료. sample peak0.59256~0.69229이며 기본gain0.55의 음악 단독 peak 상한은 약0.38076이다. RMS는 LUFS나 최종 청감 인수가 아니다.
- Editor를 호출하지 않은 오프라인 Roslyn 컴파일에서 준비 Presentation, 새 Editor 도구/검사, Bootstrap+AppRoot hook을 확인했다. 기존 CoreNetworkBuild IsSceneObject obsolete 경고2개와 오디오 오류0을 구분한다. 실제 Unity import·실행 검증은 아니다.
- 실제 C# shuffle selector100묶음/400draw의 묶음 내 중복 없음·경계 연속 반복 없음·null/중복 후보 제거·빈 목록 silence 반환을 별도 프로세스에서 확인했다.
- 실제 Unity 컴파일 오류0/경고0, GameplayMusicTests8/8 및 기존 GameAudioTests6/6 통과. authored9곡/입고, 실제 AudioSource의 짧은 clip DSP 연결, 빠른 phase 예약 취소, 지연 데이터 로드에도 기존 outgoing fade 종료 시각 유지, 빈 Night 목록의 Day fallback 금지, Spaceship loop, 동일 context/볼륨 설정의 위치 보존과 종료를 검사했다. 데이터 로드 지연은 주입 fixture이고 AudioSource/DSP는 실제 Unity다.
- 실제 Stage_1의 로컬 Field actor에서 Day3OrbitalExchange/Day2SteelFrotier가 각각 재생되는 것을 확인했다. 전술 카메라 target으로 변경한 동안도 Day context를 유지했고 활성 Listener는1개였다. 원본 곡 끝까지 기다린 장시간 검수나 청감 인수는 아니다.
- 후속 실제 재생에서 Main→허브13번(loop)→Day3→패배 시 None→재시작 Day4→Host의 `MatchDayNightCycle.TryAdvancePhase()`→Night2를 확인했다. 허브에서 메뉴를 왕복해도 재생 위치는13.788→54.684초로 진행했고 활성 Listener는1개였다. 실제 출력 PCM peak는 허브0.10015, Day0.15528, Night0.11448이었다. 이는 출력 신호 확인이며 스피커 청감 인수는 아니다.
- 실제 낮밤 전환 CSV에서 요청 후 마지막 유효 Day gain은 약0.8533초, 첫 Night gain은 약0.1707초였고 최종에는 Night2만 남았다. DSP 버퍼·Editor 표본 중복이 있어 설정0.8초와 요청 기준 관측 시간을 구분한다. 실제 출입 API로 방문하면 Spaceship13/필드 HUD 숨김, 복귀하면 Night1/필드 HUD 복원을 확인했다. 장시간 UI 검수에서만 적 생성·적 오브젝트를 일시 정지했고 Play 종료로 복구했으며 Scene이나 난이도는 저장하지 않았다.
- URP 통합 후 실제 원곡을 정상 속도로6분 관찰했다. Day3OrbitalExchange가 자연 종료한 관찰205.3278초에 같은 Day4FrontierRelay로 이어졌고 이때 실제 Day 잔여 시간은9.3583초였다. 214.6831초에 자연 Night로 바뀌었고214.7837초에 Night3GhostCircuit가 선택됐다. 데이터 로드 중 약0.1초의 빈 CurrentClip 표본과 fade 구간을 구분한다. 전환1초 이후 양의 볼륨을 가진 Day 표본은0개였고 실제 출력 PCM peak0.34243을 확인했다. 음원 seek·곡 단축·게임 시간 배속·강제 phase 변경을 사용하지 않은 관찰이다. CSV7,158행/359.9751초를 보존했다.
- URP Main→Credits→Main native 왕복을 두 번 확인했다. Credits 중3번 Another Journey가 재생되고 gameplay context는 None, Main Intro는 정지한다. 마지막 Main 복귀 시 기존 Intro 위치에서 재개했다. Intro는179.88초이고 Ambient Source가 scheduled 상태에서 isPlaying=true/timeSamples=0일 수 있으므로 이를 동시 가청 재생으로 판단하지 않았다. 정상 프레임 Main 출력 PCM peak0.21184를 확인했다. 일회 CLI 즉시 읽기의0 표본은 무음 판정 근거로 사용하지 않는다. Credits 마지막 명단은 기존55초 시간표를 유지한다.
- Main 설정에서 native 마우스로 Music0%를 적용할 때 등록 Music Source의 mute=true, 취소 시 원래100%/mute=false 복원을 확인했다. Client·Standalone·장치 청감 검수는 수행하지 않았다. 원본9곡 총425MiB는 기존 WAV 정책대로 한 벌만 입고했고 임의 LFS 전역 설정을 만들지 않았다.
- 검증 JSON은 공유 제외 `Docs/Local/AudioDayNight20261113`에 있다. 원곡 관찰 CSV와 URP Main/Credits 증거는 `Docs/Local/Validation/Tactical20261113`에 있다. 사용자 마감 지시에 따라 이번 목표에서 함께 요청한 OST 연결은 UI와 별도 커밋으로 정리한다. 추후 PR에는 새 음원/meta/library와 AudioManager/AppRoot 진입점, 이 기록 및 공용 Audio API를 함께 포함해야 한다. 푸시·PR은 이번 마감 범위에 포함하지 않는다.
