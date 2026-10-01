# GitHub Actions CI 설정

`PR CI`는 두 단계로 동작한다.

1. `Repository checks`: 별도 Secret 없이 항상 실행
2. `Unity EditMode test and Windows build`: Unity License 설정 후 실행

## 항상 실행되는 검사

- Unity 프로젝트 필수 폴더와 파일
- Unity 버전 `6000.3.23f1`
- `Force Text`와 `Visible Meta Files` 설정
- 활성화된 Build Scene과 Scene `.meta`
- `Assets` 아래 모든 파일·폴더의 `.meta` 쌍
- 대상 파일이 없는 고아 `.meta` (단, `folderAsset: yes`인 빈 폴더 메타는 허용)
- `.gitmodules` 연결이 누락된 중첩 저장소 / 잘못된 submodule
- Git에 들어가면 안 되는 `Library`, `Temp`, `Logs`, `obj`, `.vs`, `.idea`, `UserSettings`
- 해결되지 않은 Merge conflict 표시
- 50MB 초과 Asset 경고

2026-09-28 기준 [활성 `main` Ruleset](https://github.com/Team-HJD-21/unity-6-the-developer/rules/23734087)의 필수 Status Check는 `Repository checks`다. 최신 `main` 기준으로 검사를 통과해야 하며, 이 검사는 Unity License가 없어도 실행된다.

## CI 실패를 Discord로 알리기

`PR CI`의 `Repository checks` 또는 선택 Unity 검사가 실패하면 `Discord failure notification` Job이 실행된다. 메시지에는 저장소, Workflow, 이벤트, 브랜치, 짧은 Commit SHA, 실행자, 각 검사 결과와 Actions 실행 링크가 포함된다. 성공·취소·단순 skip에는 알리지 않으며 Discord 전송 실패가 기존 CI 결과를 바꾸지는 않는다.

### 1. Discord Webhook 만들기

1. GitHub 알림용 Discord 채널에서 `채널 편집 → 연동 → 웹후크`로 이동한다.
2. 새 Webhook을 만들고 해당 채널을 선택한다.
3. **Webhook URL 복사**를 누른다.

Webhook URL은 메시지를 보낼 수 있는 비밀 값이다. 채팅, Issue, PR, Workflow YAML에 직접 붙이지 않는다. 노출됐다면 Discord에서 즉시 삭제하고 새로 만든다.

### 2. GitHub Secret 등록

저장소 `Settings → Secrets and variables → Actions → New repository secret`에서 다음 값을 등록한다.

| 이름 | 값 |
|---|---|
| `DISCORD_WEBHOOK_URL` | Discord에서 복사한 Webhook URL |

Secret이 없거나 fork에서 온 PR이라 Secret을 사용할 수 없으면 알림 단계는 경고만 남기고 건너뛴다. 저장소 내부 팀 브랜치의 push/PR에는 등록된 Secret을 사용할 수 있다.

### 3. 확인

이 Workflow가 포함된 브랜치를 push한 뒤 실제 CI 실패가 발생하면 Discord 채널에 `PR CI 실패` 메시지가 올라온다. Webhook URL 자체를 출력하거나 테스트 메시지에 붙여 넣지 않는다.

Discord Webhook 공식 문서: https://docs.discord.com/developers/resources/webhook

## 빈 폴더 .meta가 CI에서만 실패했던 이유

Git은 빈 폴더를 저장하지 않는다. 로컬의 빈 TextMesh Pro 폴더는 원격 checkout에서 사라져도 폴더의 `.meta`는 남는다. 따라서 파일 존재 여부만 검사하면 정상적인 폴더 메타를 고아 Asset으로 오판한다.

`.github/scripts/validate_meta.py`는 `folderAsset: yes`인 폴더 메타를 허용한다. 일반 Asset 파일 누락, `.meta` 누락, 부모 폴더 메타 누락, 파일/폴더 종류 불일치는 계속 실패한다. Asset이나 GUID를 삭제하거나 새로 생성할 필요가 없다.

로컬에서도 동일한 검사를 실행할 수 있다 (Python 3 필요).

```sh
python -B -m unittest discover -s .github/scripts -p 'test_*.py' -v
python -B .github/scripts/validate_meta.py
```

## CI가 실패해도 push가 되는 이유와 main 보호

Actions는 push/PR 이벤트를 받은 **뒤에** 실행된다. 작업 브랜치에 push하는 것과 `main`에 반영하는 것은 다르다. 작업 브랜치 push는 허용하고, CI가 실패한 변경의 `main` 반영을 차단한다.

2026-09-28 기준 원격 `main`에는 `Project Default Branch Ruleset`이 **Active** 상태로 적용된다. 예전 `CI Approval` Ruleset은 **Disabled**이며 Merge 조건에 적용되지 않는다. 실제 GitHub 설정이 바뀌면 이 문서보다 [Ruleset 화면](https://github.com/Team-HJD-21/unity-6-the-developer/rules/23734087)을 우선 확인한다.

- PR을 통해 Merge해야 하며, 승인 1명과 Code Owner 승인이 필요하다. 현재 [CODEOWNERS](CODEOWNERS)는 모든 파일에 `@hjd5031`, `@yanggogi1423`를 지정하므로 두 사람 중 한 명이 승인하면 된다. PR 작성자는 자기 PR을 승인할 수 없다.
- 새 검토 가능 커밋을 push하면 기존 승인이 해제된다. Review 대화를 모두 해결해야 한다.
- 필수 Status Check는 `Repository checks`이며 최신 `main` 기준으로 통과해야 한다. Force push와 기본 브랜치 삭제는 금지된다.
- 사람 협업자가 없는 Copilot PR은 추가 승인을 요구할 수 있다. Copilot 자동 리뷰는 Code Owner 승인을 대체하지 않는다.

`UNITY_CI_ENABLED`가 꺼져 있다면 선택 Unity Job은 실행되지 않으며, 현재 필수 Status Check에도 포함돼 있지 않다. Unity Job을 켜고 검증을 마친 뒤 필수 검사에 추가할 수 있다. 조건에 의해 skip된 Job은 실제 Unity 검증 통과를 뜻하지 않는다.

사용 흐름: `작업 브랜치 push → PR → Repository checks 성공 → Code Owner 승인 → Review 대화 해결 → Merge`. 실패하면 작업 브랜치에 수정 commit을 push하고 CI와 승인을 다시 확인한다.

로컬 pre-push hook으로 일부 오류를 더 빨리 확인할 수는 있지만, 개발자별 설치가 필요하고 우회할 수 있으므로 서버 Ruleset을 대체할 수 없다.

공식 안내: [보호 브랜치와 필수 상태 검사](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches)

## 실제 Unity Test와 Windows Build 켜기

GitHub 저장소에서 다음 위치로 이동한다.

`Settings → Secrets and variables → Actions`

### Variables

| 이름 | 값 |
|---|---|
| `UNITY_CI_ENABLED` | `true` |

### Repository secrets

Unity Personal License 기준으로 다음 Secret을 등록한다.

| 이름 | 내용 |
|---|---|
| `UNITY_LICENSE` | GameCI 방식으로 발급한 Unity License 내용 |
| `UNITY_EMAIL` | Unity 계정 Email |
| `UNITY_PASSWORD` | Unity 계정 Password |

세 항목 중 하나라도 없으면 Unity Job은 어떤 Secret이 빠졌는지만 표시하고 실패한다. Secret 값 자체는 출력하지 않는다.

설정이 완료되면 Branch rule에 `Unity EditMode test and Windows build`도 Required status check로 추가할 수 있다.

## 현재 Build 대상

`ProjectSettings/EditorBuildSettings.asset`에서 활성화된 Scene 전체를 Windows `StandaloneWindows64`로 Build한다. 결과물은 `TheDeveloper-Windows` Artifact로 7일간 보관한다.

Unity 버전을 올릴 때는 다음 두 곳을 함께 변경한다.

- `ProjectSettings/ProjectVersion.txt`
- `.github/workflows/pr-ci.yml`의 `UNITY_VERSION`

GameCI 참고 문서:

- https://game.ci/docs/github/test-runner/
- https://game.ci/docs/github/builder/
