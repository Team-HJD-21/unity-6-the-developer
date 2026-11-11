# Core 서비스 구현 현황 문서 검토

작성일: 2026-11-12. PR: [#702](https://github.com/Team-HJD-21/unity-6-the-developer/pull/702), `feature/core → main`. 코드 대조 기준: main `2cbc4542b`.

변경은 문서뿐이다. [Core 구현 현황](../../02_architecture/01_core/core-implementation-status.md)에 실제 사용할 수 있는 서비스와 남은 소비자 연결을 적고 문서 목차·실행 계획에서 연결한다.

- CameraManager는 `MatchCameraFollow.Awake/Bind → CameraManager.Attach/RebindPlayer → PushView → lease Dispose`로 실제 연결돼 있다. #694의 기존 Editor/Play 13/13·별도 Host/Client 기록과 제품 컷씬 인수를 구분한다.
- 새 AudioManager는 없다. 기존 FieldTurret 호출은 주석이다. CameraManager의 AudioListener 선택을 BGM/SFX 관리 완료로 안내하지 않는다.
- CU 시작 설정은 #701로 반영됐으나 Enemy 제한·지형 마스크·전체 전장 Save를 함께 완료 처리하지 않는다. 나머지 API도 코드 존재/실제 소비/인수 범위를 구분한다.
- 현재 코드를 그대로 읽은 문서 동기화이며 Runtime·Scene·Prefab·패키지·네트워크/저장 형식은 변경하지 않았다. 새로운 Unity 실행 테스트는 수행하지 않았다.

로컬 Markdown 링크와 diff 공백 검사를 통과했다. TeamHJD Docs의 같은 기술 안내도 문서·검색·미디어 검사와 production build를 통과했다. 실제 새 게임 기능이나 54개 요구 전체의 런타임 시험이 추가된 것은 아니다.
