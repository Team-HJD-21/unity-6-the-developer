## 요약

무엇을 왜 변경했는지 1~3줄로 적어주세요. PR 제목은 `type(area): short English summary` 형식을 사용합니다.
예: `feat(turret): add level upgrade flow`, `fix(enemy): restore target selection`, `docs(core): clarify review rules`

- 변경 내용을 작성하세요.

## 관련 Issue

- Closes #번호 — 이 PR로 완료 조건을 충족하는 Issue만 적습니다.
- Relates to #번호 — 관련은 있지만 이 PR에서 끝나지 않는 Issue를 적습니다.

## 개발 영역

- [ ] 공통
- [ ] Planet
- [ ] Spaceship
- [ ] Planet ↔ Spaceship 연결

## 변경 종류

- [ ] 기능
- [ ] Bug 수정
- [ ] Code 구조 개선
- [ ] 성능 개선
- [ ] 설정 / Build / CI
- [ ] 문서
- [ ] Asset

## 담당

- 주 담당: Core·Player/UI·World/Weather·FieldTurret·Enemy/Encounter
- 지원 담당:
- 추가 검토 요청자(선택):

> 공용 계약·Scene·Save·Build·Network 변경은 해당 영역 담당자에게도 검토를 요청합니다.

## 핵심 변경 내용

- 코드 진입점:
- 주요 실행·데이터 흐름과 관련 참조·의존성:
- 먼저 볼 파일과 이유:
- 생략해도 되는 파일과 이유(해당 시):

문서·설정만 변경한 경우 적용되는 경로와 확인할 파일을 적어주세요.

<details>
<summary>영향받는 항목</summary>

- Scene:
- Prefab:
- Script / Assembly:
- Data / Save:
- UI / Audio / Asset:
- Network / Authority:
- 제외 범위 또는 후속 작업:

</details>

<details>
<summary>확인 결과</summary>

### 직접 확인

- [ ] 해당 기능을 PlayMode에서 확인했다.
- [ ] 관련 Core Loop를 다시 실행했다.
- [ ] 새 Console Error가 없다.
- [ ] 필요하면 Windows Development Build를 확인했다.

해당하지 않거나 실행하지 않은 항목은 이유를 적어주세요.

확인 순서와 결과:

1.
2.
3.

### CI

- 필수 `Repository checks`: PR 생성 후 결과 기입
- Unity EditMode Test / Windows Build: 실행 결과 또는 미실행 이유 기입

`Repository checks`는 `.meta`, 프로젝트 구조, 생성 파일, 충돌 표시 등을 검사합니다. Unity 검증 Job은 설정에 따라 실행되며 현재 필수 Status Check가 아닙니다.

</details>

## Screenshot / Video

Gameplay, UI, Map, Spaceship, VFX 변경이면 가능하면 첨부합니다.

- Before:
- After:

## 위험과 되돌리는 방법

- 예상되는 영향:
- 문제가 생기면 되돌릴 범위:

<details>
<summary>최종 체크</summary>

- [ ] PR은 한 가지 목적에 집중한다.
- [ ] PR 제목이 `type(area): short English summary` 형식이다 (`feat`, `fix`, `refactor`, `docs`, `test`, `ci`, `chore`, `perf` 등).
- [ ] 임시 Log와 Debug Code를 제거했거나 유지 이유를 적었다.
- [ ] 새로 추가하거나 이동한 Unity Asset과 `.meta`가 함께 포함됐다.
- [ ] `Library`, `Temp`, `Logs`, `obj`, `.vs`, `UserSettings`를 포함하지 않았다.
- [ ] 관련 문서나 Data 변경이 필요하면 함께 갱신했다.

</details>

## 병합 전 확인

- [ ] 최신 `main` 기준으로 필수 `Repository checks`가 통과했다.
- [ ] 현재 변경에 대해 쓰기 권한이 있는 리뷰어 2명의 유효한 승인을 받았다. 그중 최소 1명은 검토 가능한 변경을 마지막으로 푸시한 사람과 다르다.
- [ ] Review 대화를 모두 해결했다.

> 병합 직전에 확인합니다. 사람에게 귀속되지 않은 Copilot PR에는 추가 승인 1건이 필요합니다. [CI 안내](https://github.com/Team-HJD-21/unity-6-the-developer/blob/main/.github/CI_SETUP.md).
