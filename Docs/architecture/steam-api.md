# Steam SDK 사용과 개발 테스트

Steamworks.NET **2025.165.0 / SDK1.65**, commit `ea51b029a5dbcb47a99d3c1fac930b811672ed32`를 UPM Git 의존성으로 고정했다. 플랫폼 초기화·사용자 식별·App 콜백/종료와 친구 공개2인 로비 API를 연결했다. 실제 NGO Steam transport·Backend ticket 검증·Steam 배포를 완료한 상태는 아니다.

## 내가 준비할 것

1. 후보 AppID **4336820**이 우리 게임인지 Steamworks 앱 관리에서 확인한다. AppID 자체는 비밀 키가 아니다.
2. 서로 다른 테스트 Steam 계정에 해당 앱 실행 권한을 준비한다. 내부 개발 계정은 Dev Comp 패키지, 외부 테스트 계정은 Release Override 키 등의 방식이다. 일반 구매용 키나 Beta branch 비밀번호만으로 미출시 실행 권한이 생긴다고 가정하지 않는다.
3. 테스트 PC에서 Steam 클라이언트에 로그인한다.2인 인수는 서로 독립된 실행 환경/계정을 사용한다.

정식 출시나 Early Access 공개는 필요 없다. 현재 AppID 번호와 실제 계정 소유권은 별개다. [Valve 내부/출시 전 테스트](https://partner.steamgames.com/doc/store/testing) · [패키지](https://partner.steamgames.com/doc/store/application/packages)

## Editor에서 실행

1. `Assets/Resources/Core/SteamAppSettings.asset`에서 AppID를 확인하고 **Use Steam**을 켠다. 기본은 기존 개발 실행을 보존하도록 꺼져 있다.
2. 프로젝트 root의 개발용 `steam_appid.txt`에도 같은 번호를 둔다. 이 로컬 파일은 Git에서 제외한다. 후보 번호를 넣어두었으며 잘못된 번호를480으로 조용히 바꾸지 않는다.
3. Play를 새로 시작한다. AppRoot가 초기화를 한 번 시도하고 `Game Debugger > Overview > PLATFORM · STEAM`에 READY 또는 실제 실패 이유를 표시한다.
4. 초기화 실패는 개발용 Fake로 대체하지 않는다. Steam 모드의 미준비 상태에서는 앱의 Scene 출격을 차단한다. 계정/설정 수정 후 진단창의 재시도 버튼을 사용한다.
5. 로비 만들기 → 다른 계정에서 로비 ID 참가 또는 친구 초대 Overlay →2인 멤버 확인 →나가기 순서로 시험한다. 초대 callback은 참가할 ID를 표시하고 명시 참가 버튼을 사용한다.

같은 설정으로 Standalone에 `-teamhjd-steam`을 주면 공유 에셋의 Use Steam을 바꾸지 않고 실제 SDK 모드를 선택할 수 있다. 개발용 직접 실행에는 EXE 옆/실행 작업 디렉터리의 AppID 파일과 native library가 필요하다. `steam_appid.txt`는 Steam depot에 배포하지 않는다. [Valve SDK 초기화/개발 실행](https://partner.steamgames.com/doc/sdk/api)

## 호출 예시

앱 화면/접속 흐름은 주입받은 `IPlatformService`를 사용한다. 게임 Feature에서 AppRoot를 전역 검색하는 방식은 쓰지 않는다.

```csharp
PlatformUser user = await platform.SignInAsync(cancellation);
// user.Provider == "steam", user.PlatformId == 실제 Steam ID 문자열
// 이 값은 NGO 연결 번호·Match 슬롯이나 Backend 인증 완료가 아닙니다.

IPlatformLobbyService lobbies = (IPlatformLobbyService)platform;
PlatformLobby lobby = await lobbies.CreateLobbyAsync(cancellation);
PlatformLobby joined = await lobbies.JoinLobbyAsync(lobbyId, cancellation);
lobbies.OpenInviteOverlay();
lobbies.LeaveLobby();
```

위 create/join은 서로 다른 경로의 예시다. 이미 로비 안에 있으면 먼저 나가야 한다. 초대 수신은 `JoinRequested`로 전달한다. 로비 ID/Owner/멤버는 `CurrentLobby` 읽기 자료로 확인한다.

## 수명과 실패 처리

- App이 SDK를 한 번 소유하고 매 Update `IPlatformRuntime.Pump()`로 callback을 처리한다. 필드 정지·배속과 무관하게 계속 진행한다. Feature가 SDK를 따로 종료하지 않는다.
- 초기화는 AppID 일치·Steam 온라인 사용자·앱 이용 권한을 검사한다. 설치/라이선스/로그인 실패를 READY로 표시하지 않는다. PlatformUser의 기존 PlayerId 속성은 호환 정보이며 실제 참가자 배정은 별도로 한다.
- 로비 요청의 Task를 취소해도 SDK 호출 자체가 없어지는 것은 아니다. 늦은 callback 참조를 보관해 그 요청이 만든/참가한 로비를 정리한다. 실시간20초 deadline·중복 요청/대기 상한을 두고 App 종료에 해제한다.
- 친구 초대 Overlay의 실제 표시·초대 수락은 Steam을 통한 Standalone 환경에서 인수한다.

## 이번 연결의 범위

| 제공한 것 | 별도 단계 |
| --- | --- |
| 실제 SDK 초기화·Steam 사용자 식별·callback·종료 | Backend의 ticket 검증·계정/재화 권한 |
|2인 로비 생성/명시 참가/초대 창 API | SteamNetworkingSockets NGO transport·검증 peer→연결 ID 대응 |
| 개발 모드/Steam 모드 선택과 진단 | Steam depot/비공개 branch 업로드·실제2계정 인터넷 인수 |

로비는 아직 UTP의 IP/port를 Steam 접속으로 바꾸지 않는다. 현재 NGO Match 준비/Scene 흐름과 연결할 transport는 다음 실제 접속 단계에서 검증한다. SDK 사용자 조회만으로 운영 온라인 인증이나 보상 검증 완료라고 표시하지 않는다.

[도입 구조/후속 단계](steam-integration-design.md) · [제공자 설치](https://steamworks.github.io/installation/) · [고정 릴리스](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.165.0)

## 실제 계정 확인 기록

AppID4336820으로 SDK가 현재 계정의 앱 이용 권한을 확인했고, 실제 로비 생성·나가기·SDK 종료가 성공했다. 실제 AppRoot의 Steam 선택/자동 초기화와 TimeScale0 상태의 callback도 확인했다. 단, 후속 반복 검사에서 Steam이 k_EResultTimeout을 반환한 실행도 있어 이를 보존하며 오류/재시도 경계를 구분한다. 성공 기록만으로 외부 서비스 안정성이나 두 계정 접속까지 검증했다고 쓰지 않는다. 실제 Standalone 결과는 최종 배치 결과를 따른다.
