# 지도 전선 제거·투명 전환·조작 잠금·확보30초

2026-11-14 사용자 후속 요청. 앞선 육각막/전선 표시 및60초 기본값보다 이 기록을 우선한다.

## 변경

- 지도와 전체도에서 터렛 연결 전선/삼각형 외곽을 그리지 않는다. 확정 점유지의 실제 경계와 사거리·체력·적 정보는 유지한다. Core의 전선/점유 계산은 바꾸지 않는다.
- 전환은 전면 육각선이 펼쳐지고 중앙부터 걷혀 가장자리만 남는다. 셀 내부는 alpha0이며 검은색 면을 추가하지 않는다. 기존 전환 시간과 입력/UI 공개 시점은 유지한다.
- 지도 조작은 점유 완료 셀만 허용한다. 미점유 대상은 가까워도 버튼에 자물쇠와 `잠김`을 표시하고 stale onClick도 차단한다. Field F의 기존 해금·근접 계약은 유지한다. 지도마커/도크의 실제 가동·대기·해금잠김은 Core 원본을 유지하고 원격 잠금은 버튼과 hover로 구분한다.
- 버튼은 파괴됨/냉각/전력 부족/처리 중 등 실제 사유를 표시한다. 긴 상태가 잘리지 않도록 기존 action/hover 사각형만 넓힌다. 잠김/전원 버튼은 기존 벡터 심볼을 사용하며 오래된 pin bitmap으로 대체하지 않는다. 신규 Panel이나 설명 인디케이터를 추가하지 않는다.
- 이전 `OutsideTerritory`는 미점유와 이탈에 함께 썼던 나가기 모양이었다. 미점유는 끊긴 빈 구역 테두리, 실제 확보 구역 이탈만 방향 화살표를 더해 구분한다. 기존 벡터 심볼/폰트/재질을 사용한다.
- 적 압박인데 누적 확보가0초면 기존 UI가 미점유만 보여 주던 결함을 수정한다. 실제 EnemyPressure는 `적 우세 · 확보 중단`, 정적 미해금 셀은 `잠긴 구역`으로 표시한다. 진행 시간이나 완료를 UI에서 생성하지 않는다.
- 사용자가 확정한 확보 유지30초를 Domain 기본값, RegionSceneBinding 기본값과 Stage_1 저장 값에 함께 반영한다. 피해 성과20, 참가자당 적2, 이탈180초와 연속 열세 상실30초는 유지한다. 저장 파일을 임의로 고치거나 별도 보상을 지급하지 않는다.

## 규칙과 진단 경계

[정적 확보 계약](../../../../02_architecture/02_match/static-region-capture.md)의 해금과 점유는 별개다. 실제 성과는 피해를 받은 적의 위치가 속한 해금 구역에 기록한다. 같은 구역에 생존 플레이어가 있고 구역 전체 적 수가 플레이어수×2 이하일 때 확보 시간이 누적된다. 이는 사거리 안의 적만 세는 규칙이 아니다.

일반 Stage_1 Host의 읽기 진단에서 피해 관측자2개, 각 Executor→RegionBinding 및 실제 EnemyHealth→Executor 구독을 확인했다. 첫 표본은 Player1/Enemy0/피해0의 NeedsCombat, 적12마리가 있는3구역은 미해금 Locked였다. Field clock은 진행 중이고 실제 HoldSeconds는30이었다.

후속으로 실제 OS 마우스 클릭 사격을 했다. 최초 카논 뒤3발은 성과가 없었으며 가림 원인은 확정하지 않았다. 같은 해금1구역에서 검수 위치만 옆으로 옮겨 가림 없는 표적에2발을 명중시키자 탄약12→10, 실제 TeamDamage0→26, 참가자1/적33/Hold0/EnemyPressure가 기록됐다. UI도 `적 우세 · 확보 중단`으로 바뀌었다. 이 단계는 진행 Restore나 피해 API를 주입하지 않았다. 원래 제보 이미지와 동일한 Match의 과거 상태를 복원한 증거로 확대하지 않는다. UI 진행 복원 fixture와 구분한다.

## 검증

- Tactical19개, 실제 Stage10개(공통1개 중복), StaticRegionCapture10개를 합친 고유38개를 검사했다. 새29초 미완료/30초 완료 경계는1인/2인 모두 통과했다. 이탈/상실/복원과 시간 비소급도 확인했다.
- 전체 Stage 실행은9/10 통과했고 기존 가상 M 입력 검사1회가 열기를 관측하지 못했다. 제품 코드를 우회 수정하지 않고 같은 검사를 단독 실행해 통과했다. 간헐 실패의 근본 원인은 확정하지 않았으며 테스트 한 묶음 전체가 한 번에 통과했다고 쓰지 않는다.
- 잠금 벡터 최종 배치 뒤 `MapLocksNearby`를 다시 실행해 미점유 근접 잠김/실제대기 상태 보존/자물쇠/오래된 클릭 차단/점유 후 켜기·실제 Host 가동을 통과했다.
- 실제 native 마우스로 지도 열기/닫기·터렛 선택·상태 hover를 수행했다. 실제 IsLocked=false인 미점유 대상의 버튼은 `잠김`/interactable=false/Locked vector였고, hover는 `대기 · 미점유/원격 잠김`이었다. 둘 다 TMP overflow=false다. 터렛 연결 전선은 지도와 전체도 모두 없었다.
- native 시작의 진입/퇴장 각각23개 정상 프레임을 별도로 기록했다. 셀 내부에 실제 배경이 그대로 남는 전면 육각선과 중앙부터 걷히는 파동을 확인했다. 전환 중 Command alpha>0 표본은 양쪽 모두0개였다. GPU 검사도 셀 내부 투명 면적과 육각선 출력·중앙 제거·정상 가장자리 잔상을 구분해 통과했다.
- 일반 Host Play에서 CaptureSettings.HoldSeconds=30, Field clock 진행, 실제 마우스 사격 뒤 TeamDamage26/Player1/적33/EnemyPressure를 읽었다. 상태 복원 fixture 없이 이 조건에서 확보가 멈추는 이유와 화면 표시를 함께 확인했다.
- 전체 Assets .meta 검사0오류와 최종 수정 범위 diff 공백 검사를 확인했다. 기존 스테이징8파일을 원본 binary patch와 대조해 보존했다. 전술 Prefab의 Unity 재저장으로 생긴 trailing whitespace만 정리했고 fileID 중복은0이며 기존 View/Scene GUID는 유지했다. Scene 저장에 함께 갱신된 Map SortingLayer 파생 index2개(-1→-2)는 같은 Layer ID와 Territory 정렬에 맞는 현재값을 보존했다. 자동 검토가 이를 이전 값으로 정리하는 변경을 거절했으며, 정렬은 변경하지 않고 작업을 완료했다.
- Play를 종료하고 원래 Startup Scene을 clean Edit Mode로 복구했다. OS 입력 설정 변경, 커밋·푸시·PR은 수행하지 않았다. Standalone 빌드·물리 키보드·긴 held-drag 인수는 별도다.

자료는 ignored `Docs/Local/Validation/TacticalRefinement20261114/`의 tests-*.json, native/samples.json 및 native-final/samples.json과 PNG, final-map.json/PNG, capture-native-confirmed.json에 보관한다. 실제30초 설정·구역 진단은 Core의 `TerritoryBoundary20261114/CONTRACT_FINDINGS.md`와 전후 JSON을 함께 확인한다.
