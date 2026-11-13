# Main과 Credits OST 연결

2026-11-13 사용자 제공 원본 WAV 세 곡을 연결한다. 음악 길이에 맞춰 컷신을 늘리거나 이름 등장 시각을 옮기지 않는다.

| 사용처 | 에셋 | 재생 규칙 |
|---|---|---|
| Main 도입 | `Assets/Audio/Music/OriginalSoundtrack/FirstContact.wav` | First Contact, 179.88초, 한 번 |
| Main 대기 | `Assets/Audio/Music/OriginalSoundtrack/TheLastStand.wav` | The Last Stand, 189.96초, 도입곡 종료 직후부터 반복 |
| Credits | `Assets/Audio/Music/OriginalSoundtrack/AnotherJourney.wav` | Another Journey, 169.2초, 컷신 시작부터 한 번 |

원본은 모두 48kHz/stereo/16bit다. 복사한 WAV의 SHA256을 제공 파일과 대조해 동일함을 확인했다. 파일의 앞뒤를 자르거나 파형을 수정하지 않는다. Unity import는 Streaming/Vorbis 품질 0.9, 원본 sample rate 보존, stereo 유지다. 긴 곡 전체를 PCM으로 메모리에 풀어 놓지 않는다.

## 실행과 편집

- `MenuSoundtrack`은 Startup의 `StartupSceneRoot`에서 GameFlow에 연결한다. Main 진입 때 도입곡을 한 번 재생하고, `AudioSettings.dspTime`과 clip sample 수로 두 번째 AudioSource를 예약한다. 프레임 Update로 도입곡 종료를 기다려 다음 곡을 시작하지 않는다.
- 재사용 Prefab은 `Assets/Prefabs/UI/Common/MenuSoundtrack.prefab`이다. `Intro`, `Ambient`, `Volume`을 Inspector에서 편집한다. 기본 Volume은 0.55다. 기존 Legacy 믹서의 gain이나 기존 음악 원본은 변경하지 않는다.
- Credits로 들어갈 때 기존 검정 Fade와 함께 Main 음량을 낮추고 재생 위치를 보존한다. Main 두 AudioSource를 멈춘 뒤 Credits OST를 재생해 음악을 겹치지 않는다. 복귀하면 보존한 도입곡 또는 대기곡 위치부터 다시 예약하고 Fade-in한다. Main을 떠나 우주선으로 진입할 때는 Main 음악을 정지한다.
- Credits 음악은 `CreditsCinematic.prefab`의 `CreditsCinematicDirector.Music`과 `MusicVolume`으로 편집한다. 파트별 효과음과 별도 AudioSource다. 파트가 바뀔 때 음악을 끊거나 다시 시작하지 않는다.
- 로고는 51초, 마지막 정적 명단은 55초부터다. 정적 명단이 표시되는 동안에도 OST는 169.2초 끝까지 이어진다. 자동 반복·자동 Scene 종료는 하지 않는다. 중도 복귀하면 Fade-out 후 음악을 정지한다.
- `CreditsCinematicDirector.Pause` 또는 seek 검수는 음악을 정지한다. `Play`는 컷신과 음악을 처음부터 시작한다. 정지 프레임 검수를 음악 재생 검증으로 간주하지 않는다.
- 적용 메뉴는 `Tools > TeamHJD > The Developer > Soundtrack > Apply original soundtrack`이다. 편집 중인 dirty Scene이 있으면 저장을 강요하거나 덮어쓰지 않고 중단한다. Main·Credits 재생성 도구에도 같은 연결을 추가했다.

실제 검증 범위는 후속 기록에 명시한다. 원본 파일 안의 음량 변화와 반복 경계의 편곡은 재생 예약의 무간격 여부와 별도로 판단한다.

## 적용 확인 범위

Unity recompile 오류/경고 0, 저장된 세 clip의 길이·48kHz/stereo·Streaming 설정, 반복 false/true/false를 확인했다. 이전 컷신의 파트 길이·이름/역할·효과음 cue·Fade·로고 길이는 동일하며 정적 명단 시작은 55초다. Startup Scene diff는 음악 Prefab과 참조 추가로 제한했다.

실제 Main Play에서 First Contact 재생과 단일 AudioListener, 두 번째 곡 예약을 확인했다. 이후 사용자가 직접 확인하겠다고 요청해 자동 계측을 해제하고 Unity Play와 음악을 유지한 채 넘겼다. 전체 곡 전환 경계·반복 청취·크레딧 진입/복귀의 수동 인수와 Standalone 빌드는 완료로 기록하지 않는다. 원본 기록은 `Docs/Local/Validation/Soundtrack20261113`에 있다.
