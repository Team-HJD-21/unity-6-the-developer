# Unity 에셋 자동 잠금

기존 `.unity`, `.prefab`, `.png.meta` 파일을 편집하면 Unity가 `origin`에 Git LFS 잠금을 요청한다. 잠금 소유자만 쓰기 가능으로 바뀐다. 스프라이트의 Import Settings는 PNG 원본과 별개로 `.png.meta`를 잠근다. 새 에셋은 작성할 수 있다.

## 저장소 관리자

1. 잠금 해제용 SSH 키의 공개 부분을 이 저장소의 관리자 GitHub 사용자 계정에 인증 키로 등록한다. 비공개 키는 저장소의 **Settings → Environments → unity-asset-lock-release**에 환경 시크릿 `UNITY_ASSET_LOCKS_SSH_KEY`로 등록한다. 환경은 `main` 브랜치만 허용한다. 이 사용자 인증 키는 해당 계정이 Git으로 접근할 수 있는 저장소에도 유효하므로 비공개 키는 이 환경에서만 사용한다.
2. 배포 후 두 계정으로 같은 프리팹을 편집한다. A만 저장할 수 있고 B는 저장이 차단되어야 한다. A가 GitHub Desktop에서 커밋과 푸시를 한 뒤 Unity를 종료한다. A의 PR을 `main`에 병합하고 **Release merged Unity asset locks**가 잠금을 풀면 B가 최신 코드를 받은 뒤 편집할 수 있어야 한다.

## 팀원

프로젝트를 업데이트한 뒤 Unity에서 한 번 열면 현재 복제본의 Git 설정과 `pre-push` 훅이 자동으로 등록된다. Git과 Git LFS는 설치되어 있어야 하며, Unity 프로세스에서 `origin`에 로그인할 수 있어야 한다. Windows는 기본 PowerShell을 사용한다. macOS·Linux는 `pwsh`가 필요하다. 별도 `core.hooksPath`를 직접 지정한 복제본은 훅을 합쳐야 한다는 메시지가 뜬다.

macOS에서는 `brew install powershell`로 설치하고 `pwsh --version`으로 확인한다. Unity와 푸시 훅은 기존 `PATH`에 이어 Apple Silicon과 Intel의 Homebrew 기본 경로를 확인한다.

편집과 커밋, 푸시는 평소대로 한다. Unity가 앱 내부에서 직접 편집 요청을 보내지 않는 화면에서는 Project 창에서 **Assets → Open for Edit (Git LFS)**를 사용할 수 있다. 런타임 게임 코드에는 적용되지 않는다.

기존 **Edit → Project Settings**에서 **Physics 2D** 또는 **Tags and Layers**의 값을 변경하면 자동으로 잠금을 얻는다. 이미 잠긴 파일의 값을 바꾸면 현재 화면의 변경이 되돌아가고 Console에 잠금 소유자가 표시된다. 받아온 `main` 이후 내 브랜치에서 커밋한 변경도, `main`에서 그 파일의 버전이 더 바뀌기 전에는 잠금을 얻어 이어서 편집할 수 있다.

폴더를 이동하거나 이름을 바꾸거나 삭제할 때는 Git에 등록된 하위 에셋의 잠금도 확인한다. 하나라도 편집할 수 없으면 작업이 차단된다.

제출할 에셋은 저장 후 전부 커밋한다. 푸시 후에도 잠금을 얻은 작업 폴더에서 편집을 계속할 수 있으며 서버 잠금은 유지된다. 같은 PR에 수정본을 푸시하면 기존 제출 태그가 새 파일 버전의 태그로 교체된다. 수정본 제출과 잠금 해제가 겹치면 기존 태그를 먼저 갱신한 작업만 진행된다. 브랜치 푸시가 실패하면 같은 커밋으로 다시 푸시할 수 있다. 잠금을 얻었을 때의 파일 내용으로 되돌려 제출하면 해당 제출 태그를 제거한다. 커밋 전에 감시기가 확인한 저장 변경을 전부 취소하면 로컬 감시기가 잠금을 해제한다. PR이 닫히거나 파일 내용을 병합 중에 바꾼 경우에는 작성자와 관리자가 작업 상태를 확인한 뒤 잠금을 해제한다. [커밋 전 변경 취소](../../Docs/05_tooling/unity-asset-locks.md#커밋-전-변경-취소)

## 동작

- GitHub Desktop과 CLI의 `pre-push`는 저장된 파일 버전을 잠금 ID와 연결하는 임시 `unity-asset-locks/*` 태그를 원격에 기록한다. 태그는 이미 GitHub에 올라간 커밋을 가리킨다.
- `main`을 갱신할 때 Actions가 현재 잠금 ID와 제출 태그를 조회한다. 태그가 가리키는 편집 시작 당시의 파일과 최신 `main`의 파일이 달라졌고, 최신 파일이 제출 버전과 일치하면 잠금을 해제하고 태그를 지운다. 스쿼시 병합과 이전 실행에서 누락된 잠금에도 이 기준을 적용한다.
- GitHub에서 **Actions → Release merged Unity asset locks → Run workflow**를 열어 `main`의 잠금을 다시 검사할 수 있다. 실행 로그에 조회한 잠금, 제출 태그, 해제 대상 개수가 출력된다. GitHub 호스팅 러너가 사용자 SSH 키로 Git과 Git LFS를 인증한다. 조회 권한 오류나 잠금 목록 불일치가 발생하면 실행이 실패로 표시된다.
- Unity가 프로젝트를 열 때와 이후 45초마다 로컬 파일 상태를 맞춘다. Project 창의 잠금 목록은 15초마다 갱신하며 Unity로 돌아오면 바로 다시 조회한다. 작성자의 PC 전원과 관계없이 서버 잠금은 해제된다. 로컬 파일의 동기화에는 Unity가 다시 열려 있어야 한다.
