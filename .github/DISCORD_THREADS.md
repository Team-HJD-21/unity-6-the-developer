# GitHub 항목별 Discord 스레드 알림

## 동작과 범위

GitHub Actions가 Discord Bot REST API를 호출한다. 별도 상시 서버나 Gateway 연결은 필요하지 않다. 기존 이슈·PR·Discussion 채널을 유지하며 신규 항목의 첫 이벤트에 부모 안내 메시지와 public thread를 만든다. 기존 항목도 전환 후 첫 이벤트에서 같은 방식으로 생성한다. 과거 댓글 전체를 재전송하지 않는다.

| 이벤트 | 전송 위치 |
|---|---|
| 이슈 생성·수정·담당·라벨·상태 변경·일반 댓글 | 이슈 채널의 해당 Issue 스레드 |
| PR 생성·수정·push 반영·담당·리뷰 요청·승인/변경 요청·일반/코드 댓글·답글·병합/종료 | PR 채널의 해당 PR 스레드 |
| 커밋 댓글 | GitHub API가 연결한 PR 스레드. 여러 PR이면 각각 전달 |
| PR CI 실패 | 해당 PR 스레드. main push와 수동 CI 테스트는 기존 CI 웹후크 유지 |
| Discussion 생성·수정·상태/답변 변경·댓글/답글 | Discussion 채널의 해당 Discussion 스레드 |

PR 일반 댓글은 GitHub의 `issue_comment` 이벤트로 오지만 `issue.pull_request`로 구분한다. 커밋에 연결된 PR이 없으면 다른 채널에 보내지 않고 Actions 기록에 남긴다. 나중에 PR이 생겼을 때 이전 커밋 댓글을 소급 재생하지 않는다.

닫힌 항목은 마지막 알림을 보낸 뒤 스레드를 보관한다. 이후 재개/늦은 댓글은 같은 스레드를 다시 사용한다. 수동으로 잠긴 스레드는 임의로 잠금을 풀거나 다른 스레드로 우회하지 않고 오류를 보고한다.

부모 메시지 footer의 저장소·종류·번호가 영구 연결 정보다. 활성/보관 스레드와 부모 메시지를 조회하여 찾으므로 Actions cache나 임시 파일 만료로 연결이 사라지지 않는다. 부모 메시지/스레드를 삭제하면 연결 증거도 사라지므로 삭제하지 않는다. 재실행은 스레드의 event receipt로 구분하며 동시 최초 생성은 Discord nonce와 메시지당 한 스레드 제약을 사용한다. 동시 이벤트를 버릴 수 있는 Actions concurrency group을 사용하지 않는다.

메시지는 봇 이름으로 전송하고 GitHub 작성자는 embed에 표시한다. 전체/개인/역할 mention은 비활성화한다. 이벤트 본문을 셸 명령으로 실행하지 않고 PR head 코드를 checkout하지 않는다. 조회·전송 실패 시 Actions를 실패 처리하며 엉뚱한 채널로 대체하지 않는다.

## 기존 채널

`.github/discord-thread-channels.json`은 비밀이 아닌 채널 ID만 보관한다.

- Server: `1547585693465976954`
- Issue: `1547739804303757433`
- PR: `1553756393041960990`
- Discussion: `1565218609754677338`

## 봇 준비

1. [Discord Developer Portal](https://discord.com/developers/applications)에서 Application을 만들고 Bot을 준비한다. 전용 봇을 권장한다.
2. Bot token을 복사하여 GitHub 저장소의 **Settings → Secrets and variables → Actions → New repository secret**에 `DISCORD_BOT_TOKEN`으로 저장한다. 채팅/Issue/PR/커밋에 붙이지 않는다.
3. OAuth2 URL Generator에서 `bot` scope와 다음 권한을 선택하여 위 서버에 초대한다: View Channels, Send Messages, Read Message History, Embed Links, Create Public Threads, Send Messages in Threads, Manage Threads. Administrator 권한은 필요하지 않다. 각 대상 채널의 override에서도 허용한다.
4. 초대 URL 형식: `https://discord.com/oauth2/authorize?client_id=APPLICATION_ID&scope=bot&permissions=326417599488&guild_id=1547585693465976954&disable_guild_select=true`. `APPLICATION_ID`는 실제 Application ID로 바꾼다. Message Content privileged intent는 사용하지 않는다.

봇이 오프라인으로 보여도 REST 방식 알림은 동작한다. 기존 웹후크 URL을 새 Secret으로 복사할 필요는 없다.

## 순서 있는 전환

현재 workflow는 `DISCORD_THREADS_ENABLED=true` 전까지 자동 전송하지 않는다. 미커밋 코드 작성만으로 설정이 가동되지는 않는다.

1. 변경을 리뷰·검사하고 기존 절차로 main에 반영한다.
2. 위 봇 등록/초대를 마친다.
3. Actions의 **Discord topic threads → Run workflow**를 main에서 실행한다. 기본 `check_only=true`는 조회만 확인하며 메시지를 보내지 않는다.
4. `check_only=false`로 한 번 더 실행한다. 각 채널에 **[TEST] / #0** 표시의 테스트 스레드가 만들어지고 테스트 메시지를 보낸 뒤 보관한다. 실제 GitHub 항목은 만들지 않는다. 세 채널에서 실제 생성/전송을 확인한다.
5. 관리자 인증된 `gh`로 아래 도구를 실행한다. 현재 main의 성공한 delivery 테스트가 1시간 안에 있어야 진행한다. backup 경로는 Git에 추적하지 않는 로컬 경로로 정한다.

```powershell
python .github/scripts/configure_discord_threads.py enable --backup D:/UnityProjects/.codex-validation/discord-hooks-before-threads.json
```

도구는 복원용 active/events만 저장하고, `DISCORD_THREADS_ENABLED=true` 설정 후 중복되는 기존 웹후크 이벤트만 제거한다. URL·token은 파일/로그에 저장하지 않는다. Notion 웹후크, 기여자 그래프, main/수동 CI 알림, 독립 milestone/sub-issue/dependency 웹후크 이벤트는 변경하지 않는다. 이번 workflow의 항목별 이벤트 범위는 위 표를 따른다.

6. 이후 실제 항목/댓글에서 올바른 채널과 동일 스레드에 오는지 확인한다. 설정 실패나 API 제한은 Actions 실행에서 확인한다.

복원은 이전 이벤트를 먼저 살린 뒤 새 workflow를 비활성화한다. 기존 메시지/스레드는 삭제하지 않는다.

```powershell
python .github/scripts/configure_discord_threads.py rollback --backup D:/UnityProjects/.codex-validation/discord-hooks-before-threads.json
```

외부 fork/Dependabot의 `pull_request_review`와 `pull_request_review_comment` 실행은 GitHub의 Secret 제공 제한을 받는다. 현재 팀 내부 브랜치 경로와 구분한다. PR lifecycle은 `pull_request_target`의 신뢰 기본 브랜치를 사용하며, 외부 head를 실행하지 않는다. CI는 신뢰 기본 브랜치의 `workflow_run` 경로다. 새 외부 기여자용 리뷰 알림까지 지원하려면 별도 신뢰 relay 경로를 먼저 설계한다.

## 로컬 확인

```powershell
python -B -m unittest discover -s .github/scripts -p 'test_discord_threads*.py' -v
```

네트워크 없는 테스트는 라우팅/중복/보관/재개/잠금/권한 분리 로직의 근거다. 실제 봇 권한과 Discord 수신 증거는 위 delivery smoke test 이후에만 확정한다.

공식 규약: [Discord threads](https://docs.discord.com/developers/resources/channel#start-thread-from-message), [Discord message nonce](https://docs.discord.com/developers/resources/message#create-message), [GitHub Actions events](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows).
