# Unity 에셋 자동 잠금

기존 `.unity`, `.prefab`, `.png.meta` 파일을 편집하면 Unity가 `origin`에 Git LFS 잠금을 요청한다. 잠금 소유자만 쓰기 가능으로 바뀐다. 스프라이트의 Import Settings는 PNG 원본과 별개로 `.png.meta`를 잠근다. 새 에셋은 작성할 수 있다.

## 저장소 관리자

1. GitHub 저장소의 **Settings → Secrets and variables → Actions**에 `UNITY_ASSET_LOCKS_TOKEN`을 등록한다. 이 토큰을 쓰는 계정은 이 저장소에서 다른 사용자의 LFS 잠금을 ID로 강제 해제하고, `unity-asset-locks/*` 태그를 삭제할 수 있어야 한다. 저장소 접근 범위를 제한한 전용 계정을 사용한다. 실제 GitHub 환경의 토큰 유형, 조직 승인, 강제 해제 권한은 서로 다른 두 계정으로 확인한다.
2. 배포 후 두 계정으로 같은 프리팹을 편집한다. A만 저장할 수 있고 B는 저장이 차단되어야 한다. A가 GitHub Desktop에서 커밋과 푸시를 한 뒤 Unity를 종료한다. A의 PR을 `main`에 병합하고 **Release merged Unity asset locks**가 잠금을 풀면 B가 최신 코드를 받은 뒤 편집할 수 있어야 한다.

## 팀원

프로젝트를 업데이트한 뒤 Unity에서 한 번 열면 현재 복제본의 Git 설정과 `pre-push` 훅이 자동으로 등록된다. Git과 Git LFS는 설치되어 있어야 하며, Unity 프로세스에서 `origin`에 로그인할 수 있어야 한다. Windows는 기본 PowerShell을 사용한다. macOS·Linux는 `pwsh`가 필요하다. 별도 `core.hooksPath`를 직접 지정한 복제본은 훅을 합쳐야 한다는 메시지가 뜬다.

macOS에서는 `brew install powershell`로 설치하고 `pwsh --version`으로 확인한다. Unity와 푸시 훅은 기존 `PATH`에 이어 Apple Silicon과 Intel의 Homebrew 기본 경로를 확인한다.

편집과 커밋, 푸시는 평소대로 한다. Unity가 앱 내부에서 직접 편집 요청을 보내지 않는 화면에서는 Project 창에서 **Assets → Open for Edit (Git LFS)**를 사용할 수 있다. 런타임 게임 코드에는 적용되지 않는다.

기존 **Edit → Project Settings**에서 **Physics 2D** 또는 **Tags and Layers**의 값을 변경하면 자동으로 잠금을 얻는다. 이미 잠긴 파일의 값을 바꾸면 현재 화면의 변경이 되돌아가고 Console에 잠금 소유자가 표시된다. 받아온 `main` 이후 내 브랜치에서 커밋한 변경도, `main`에서 그 파일의 버전이 더 바뀌기 전에는 잠금을 얻어 이어서 편집할 수 있다.

폴더를 이동하거나 이름을 바꾸거나 삭제할 때는 Git에 등록된 하위 에셋의 잠금도 확인한다. 하나라도 편집할 수 없으면 작업이 차단된다.

제출할 에셋은 저장 후 전부 커밋한다. 첫 푸시 때 해당 파일은 로컬에서 읽기 전용으로 바뀌고 서버 잠금은 유지된다. 초기 푸시가 실패하면 같은 커밋으로 다시 푸시할 수 있다. 커밋 전에 감시기가 확인한 저장 변경을 전부 취소하면 로컬 감시기가 잠금을 해제한다. PR이 닫히거나 파일 내용을 병합 중에 바꾼 경우에는 작성자와 관리자가 작업 상태를 확인한 뒤 잠금을 해제한다. [커밋 전 변경 취소](../../Docs/05_tooling/unity-asset-locks.md#커밋-전-변경-취소)

## 동작

- GitHub Desktop과 CLI의 `pre-push`는 저장된 파일 버전을 잠금 ID와 연결하는 임시 `unity-asset-locks/*` 태그를 원격에 기록한다. 태그는 이미 GitHub에 올라간 커밋을 가리킨다.
- `main` push에서 Actions가 실제로 바뀐 경로와 제출된 버전을 대조한다. 일치할 때만 같은 ID의 잠금을 풀고 태그를 지운다. 파일 버전이 같으면 스쿼시 병합도 동작한다.
- Unity가 프로젝트를 열 때와 이후 45초마다 로컬 파일 상태를 맞춘다. Project 창의 잠금 목록은 15초마다 갱신하며 Unity로 돌아오면 바로 다시 조회한다. 작성자의 PC 전원과 관계없이 서버 잠금은 해제된다. 로컬 파일의 동기화에는 Unity가 다시 열려 있어야 한다.
