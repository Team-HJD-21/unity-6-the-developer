# Unity 에셋 자동 잠금

Unity 프로젝트를 한 번 열면 로컬 Git 설정과 푸시 훅이 등록된다. 팀원은 Git과 Git LFS를 설치하고 Unity에서 원격 저장소에 접근할 수 있어야 한다. macOS와 Linux에는 pwsh도 필요하다.

- 기존 Scene·Prefab과 PNG Import Settings를 편집하면 해당 파일의 Git LFS 잠금을 얻는다. PNG Import Settings는 .png.meta 경로를 기준으로 잠근다.
- Edit → Project Settings에서 Physics 2D 또는 Tags and Layers의 값을 바꿔도 같은 방식으로 잠근다. 다른 팀원이 소유한 경우 설정값을 되돌리고 Console에서 잠금 소유자를 안내한다.
- CLI와 GitHub Desktop 푸시는 훅을 통해 제출한 파일 버전을 잠금과 연결한다. 제출 버전이 main에 반영되면 GitHub Actions가 서버 잠금을 해제한다. Unity가 다음에 열리거나 열린 에디터가 새 상태를 확인하면 로컬 파일 속성이 동기화된다.
- 도입 검증은 계정 두 개로 같은 파일을 편집해 한 사람만 저장할 수 있는지 확인한 뒤, 첫 작성자가 Unity를 닫고 main에 병합해 서버 잠금이 해제되는지 확인한다.

설치·권한·잠금 해제 조건은 [UnityAssetLocks 운영 안내](../../tools/UnityAssetLocks/README.md)를 따른다.
