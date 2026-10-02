# Sprint 1 — Stage 1 PoC

기준 회의: 2026-09-19 정기회의 · 2026-09-26 검토 방향 보충
실행 기간: 2026-09-19 ~ 2026-10-02
팀 플레이 테스트: 2026-10-02
피드백 회의: 2026-10-03 예정
상태: 실행 기준

[문서 목차](../README.md)

> Sprint Goal: 최소 Graybox에서 전제 1(동적 전선)과 전제 2(지역 해금)가 각각 재미있는 전력·전선 선택을 만드는지 시험한다. 2026-10-02 통합 Build에서는 가능한 시제품의 플레이 감각과 기술 위험을 관찰한다. 두 안의 구현 순서·병행 범위와 최종 채택안은 미정이다.

## Sprint 1 2주차 목표 — 마무리 주간 (2026-09-28 ~ 2026-10-02)

> 이 절의 영역별 작업은 2026-09-28 현재 GitHub `Stage 1 PoC` 마일스톤의 열린 Issue를 모아 보여준다. 각 Issue가 실제 담당·진행 상태의 기준이며, 표는 새 담당 배정이나 완료 선언이 아니다. Sprint 1의 공통 목표는 전제 1(동적 전선)과 전제 2(지역 해금)의 재미를 시험하는 것이다. 둘 중 무엇을 먼저 또는 함께 구현할지는 정해지지 않았다.

| 영역 | 2주차에 확인할 작업 | 담당 / Issue |
| --- | --- | --- |
| 공통 통합 | 10월 2일 같은 Build에서 전력 배분, 터렛·Enemy 전투, 거점·전선 변화를 플레이하고 관찰 결과를 기록한다. Issue에 적힌 임시 승패 조건은 최근 결정 문서에서 확정되지 않았으므로 확정 기준으로 취급하지 않는다. | 전체 팀 · [#420](https://github.com/Team-HJD-21/unity-6-the-developer/issues/420) |
| Core·전선·도구 | Turret snapshot을 받아 점유 topology와 경계 데이터를 만들고 Scene View에서 관찰한다. 레거시 경로 제거는 Owner가 검토·승인한 항목에 한해 진행한다. | A · [#443](https://github.com/Team-HJD-21/unity-6-the-developer/issues/443), [#446](https://github.com/Team-HJD-21/unity-6-the-developer/issues/446), [#441](https://github.com/Team-HJD-21/unity-6-the-developer/issues/441) |
| Player | 새 스킬 입력·쿨다운·기본 피드백을 구현하고 터렛 보조 수단으로 유효한지 확인한다. P2 품질 개선 Issue이므로 공통 PoC 통합 완료 조건과는 구분한다. | B · [#452](https://github.com/Team-HJD-21/unity-6-the-developer/issues/452) |
| Turret·전력 | AI·전선용 읽기 전용 snapshot, 해금·활성화 제한, 업그레이드와 Enemy 연동을 구현·검증하고 통합 담당자에게 결과를 전달한다. 파괴 전력 반환 여부와 비율은 미정이며 API 입력값만 열어둔다. | D · [#422](https://github.com/Team-HJD-21/unity-6-the-developer/issues/422), [#425](https://github.com/Team-HJD-21/unity-6-the-developer/issues/425), [#426](https://github.com/Team-HJD-21/unity-6-the-developer/issues/426), [#427](https://github.com/Team-HJD-21/unity-6-the-developer/issues/427), [#430](https://github.com/Team-HJD-21/unity-6-the-developer/issues/430), [#432](https://github.com/Team-HJD-21/unity-6-the-developer/issues/432) |
| Enemy | Player·Turret·Stage 연동 및 네트워크 동작을 검증하고, 선행 오류를 정리한 뒤 Spawner 동작을 확인한다. 최종 Wave 구성이나 난이도 조정은 범위에 포함하지 않는다. | E · [#394](https://github.com/Team-HJD-21/unity-6-the-developer/issues/394), [#395](https://github.com/Team-HJD-21/unity-6-the-developer/issues/395), [#403](https://github.com/Team-HJD-21/unity-6-the-developer/issues/403), [#437](https://github.com/Team-HJD-21/unity-6-the-developer/issues/437) |
| World·Map | 현재 열린 Issue 중 C 담당 Stage 1 PoC 마일스톤 작업은 확인되지 않았다. 지도 시연 #450과 Stage 1 이식 #451은 M1로 등록되어 있으므로 이 표의 Stage 1 목표에 포함하지 않는다. Sprint 1에 포함할지 여부는 별도 분류가 필요하다. | C · [#450](https://github.com/Team-HJD-21/unity-6-the-developer/issues/450), [#451](https://github.com/Team-HJD-21/unity-6-the-developer/issues/451) |

이 주차 목표는 **Sprint 1의 2주차**를 뜻하며 `Sprint 2`를 새로 정의하지 않는다. 주차는 마일스톤과 Issue 분류를 읽기 쉽게 묶은 것이고, Issue의 상태·담당자·완료 조건을 대신하지 않는다.
## 1. 검증할 핵심 가설

1. 직접 전투보다 터렛 중심 전선 운영이 게임의 고유한 재미로 작동한다.
2. Player가 주 화력이 아니어도 수리·보조 공격·군중 제어로 충분히 개입한다고 느낀다.
3. 모든 터렛을 켤 수 없는 전력 한도가 실제 전략 선택을 만든다.
4. 전제 1에서 개별 터렛 운용에 따른 전선 전진·후퇴가 재미와 명확한 피드백을 만든다.
5. 전제 2에서 지역 해금과 지역별 터렛 운용이 재미와 명확한 진척감을 만든다.
6. Enemy가 Player만 추격하지 않고 약한 터렛 거점과 핵심 시설을 합리적으로 공격한다.

## 2. 최소 플레이 흐름

```text
Spaceship에서 Stage 1으로 이동
→ Player 이동·기본 공격, 미리 배치된 터렛 활성화, Enemy 전투 연결
→ 전력 제약 아래 전제 1의 전선 변화 또는 전제 2의 지역 해금 시험
→ 디버그 표시로 전선·진척을 관찰하고 재미·이해도 기록
→ Player 사망은 디버그 부활 버튼 또는 무적으로 시험
```

## 3. 완료 기준

- [ ] 최소 Graybox Map에서 Player가 출격할 수 있다.
- [ ] 미리 배치된 터렛을 활성화하고 기본 전투에 사용할 수 있다.
- [ ] 전력 한도 때문에 모든 터렛을 동시에 켤 수 없다.
- [ ] 전제 1의 전선 변화와 전제 2의 지역 해금에 대해 각각 검증할 재미·이해도 질문을 정한다.
- [ ] 10월 2일 통합 가능한 시제품에서 전선 변화 또는 지역 해금의 플레이 감각을 관찰하고 결과를 기록한다. 나머지 후보의 시험 순서·일정은 별도 결정한다.
- [ ] Enemy가 목표를 선택하고 터렛 또는 핵심 시설을 공격한다.
- [ ] Player가 약한 전선으로 이동해 전투에 개입할 수 있다.
- [ ] Player 사망을 정식 패배 화면 없이 디버그 부활 버튼 또는 무적으로 시험한다.
- [ ] 시험한 후보의 관찰 결과와 두 후보에 남은 기술 위험을 기록한다. 승리 판정과 결과·성장 흐름은 완료 조건이 아니다.
- [ ] 2026-10-02에 전체 팀이 같은 Build로 통합 플레이 테스트를 수행한다.

## 4. 역할과 결과물

| 담당 | 핵심 역할 | 구체 결과물 |
| --- | --- | --- |
| A — 양현석 | PoC 범위·계약·통합 | Sprint 1 Issue, 인터페이스, 전제 1·2 관찰 기준과 Player 지원 행동, 통합 Build |
| B — 이영빈 | Network-aware 조사와 Combat 지원 | Unity Netcode/RPC/동기화/권한 최소 가이드, 향후 전환 위험 목록 |
| C — 김진태 | World·Mission·전선 확장 | 거점 확장 Graybox, 거점 확보·상실에 따른 전장 변화, D/E 데이터 연동 규격, Spaceship 레퍼런스 |
| D — 황재동 | Turret System | 터렛 인스턴스 관리, 기존 코드 정리, 위치·활성·체력·화력·거리·위협도 제공, 파괴 시 전력 손실률 API 입력값 공개 |
| E — 조수빈 | Enemy AI | Region 단위 공격 목표 선택을 우선 시험하고, 타깃 점수 세부 조정은 후순위로 둔다 |

## 5. 통합 순서

```text
D · 터렛 상태·평가 데이터
        ↓
E · 목표 선택 AI와 공격 결과
        ↓
C · 거점 확보·상실과 전선 확장
        ↓
A · 전제 1·2 관찰·Player 지원·통합 Build
        ↓
전체 팀 · 2026-10-02 Playtest
```

B의 Network-aware 조사는 위 통합을 막지 않는 병렬 작업이다. 실제 실시간 협동 구현은 이번 범위에 포함하지 않는다.

## 6. 의도적으로 제외

- 완성도 높은 Tilemap, 최종 Art와 UI polish
- 모든 Planet, Enemy, Turret 제작
- 실시간 협동 멀티플레이 완성
- 최종 경제 Balance와 장기 성장 확정
- Spaceship 꾸미기, Pet, Mini-game
- 정확한 낮/밤 길이와 최종 환경 효과

2026-09-26 회의에서는 동적 전선과 지역 해금을 우선 검토하고 기존 스테이지 방식을 뒤로 두었다. 9월 27일에는 Stage 1 PoC의 우선 목적을 두 후보의 재미 검증으로 명확히 했다. 두 우선 후보 가운데 최종안을 정하거나 병행 구현 범위·일정을 확정하지는 않았다. 회의에서 새 마감이나 Sprint 담당 재배정도 없었다. 보관된 [9월 22일 비교 문서](../archive/2026-09-22-progression-options.md)와 [9월 26일 회의 근거](../archive/2026-09-26-stage1-planning.md)를 참고한다.

Stage 1에서는 정식 패배 화면 대신 디버그 부활 버튼 또는 무적 상태로 플레이어 사망을 처리한다. Region 단위 적 목표 선택은 조수빈이 우선 시험한다. 이는 임시 PoC 동작이며 최종 승패·체크포인트·전선 규칙 확정이 아니다.

## 7. 아직 결정하지 않은 항목

9월 27일 기준으로 전제 1·2를 각각 재미 검증 대상으로 삼되, 실험 순서·같은 Build에서의 병행 범위·일정은 정하지 않았다. Product Vertical Slice의 연구·결과 성장·다음 Mission과 기존 웨이브 진행은 이번 PoC의 완료 조건이 아니다.

- PoC 및 최종 승리 조건: 마지막 거점, Boss, 별도 목표 중 선택
- 최종 패배 및 후속 처리: Player 사망과 Control Unit 파괴를 논의했으나 두 결과와 유지·초기화 범위는 미정
- Player 공격 조작과 수리·보조 공격·군중 제어의 비중
- 동적 전선·지역 해금 중 최종 진행 방식과 기존 스테이지 방식 재검토 여부
- 전제 1·2의 순차 또는 병행 검토 방법과 일정
- 터렛 단위와 region 단위 전선 계산 중 채택할 방식
- 터렛 파괴 시 실제 전력 반환 여부·손실률과 점유율 처리. 손실률은 API 입력값으로 열어두고 수치는 미정
- 터렛 Upgrade와 Spaceship Research의 역할 분담
- PoC 한 판의 목표 시간과 낮/밤 주기
- 다음 Planet 성장 이월과 이전 Planet 재방문 보상
- PoC 코드를 프로덕션으로 이식할 범위

## 8. Issue와 PR 규칙

- 모든 업무를 GitHub Issue로 만들고 담당자, 영역, 우선순위와 완료 조건을 기록한다.
- PR에 관련 Issue와 테스트 결과를 남긴다.
- 다른 사람의 Scene을 직접 수정하지 않는다.
- 같은 Scene에 필요한 기능은 가능한 한 Prefab 또는 분리된 Adapter로 전달한다.
- 막힌 내용은 오래 추측하지 않고 팀에 공유한다.

## 9. Playtest 기록

2026-10-02 Build/Commit, 참가자, 관찰 결과와 Blocker를 여기에 연결한다. 2026-10-03 회의에서 각 가설을 통과·수정·폐기로 판정하고 [PROJECT_PLAN.md](../product/PROJECT_PLAN.md) Decision Log와 다음 Backlog를 갱신한다.
