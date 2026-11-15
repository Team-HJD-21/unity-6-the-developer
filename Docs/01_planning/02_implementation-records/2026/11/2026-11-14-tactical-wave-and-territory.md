# 전술 전환·연속 영역·필드 상태 표시 후속

2026-11-14 사용자 재요청. 이전의 얇은 스캔/즉시 카메라 복귀보다 이 기록의 전환 순서를 우선한다.

## 변경

- 화면 전체 육각막이0.12초에 덮이고0.06초 유지할 때 카메라를 교체한다. 중앙 파동이 진입0.42초/종료0.30초에 바깥으로 걷힌 뒤 지도/필드 조작 UI를 공개한다. 합계 진입0.60초/종료0.48초다. 파동의 얇은 경계에만 색 분리와 패턴 굴절을 더했다. Scene texture 자체를 굴절시키지는 않는다.
- ModeEffect.Update(-300)에서 handoff를 확정하고 Controller(-250)/일반 HUD보다 먼저 처리한다. 이전 전투 슬롯/무기가 한 프레임 남지 않게 한다. 입력 lease는 시작부터 끝까지 소유하며 취소/재요청은 현재 막에서 이어진다. 외부 메뉴/사망/씬 비활성은 애니메이션을 기다리지 않고 즉시 정리한다.
- 내부 셀 테두리/확보 사선을 없애고 연속 mask 색면과 부드러운 feather로 영역을 구분한다. 실제 CapturedCells 외곽의 월드 XY는 유지한다. Field와 같은 공유 변 제거 외곽선을 사용하고 별도 곡선/삼각형 전선으로 점유를 대체하지 않는다.
- `가까이에서 조작`은 여러 차단 이유를 한 문구로 표시하던 부분이었다. 도크 위 상시 문구를 제거하고 실제 사유는 상태 아이콘 hover 때만 보여 준다. 확정 점유 지역의 개별 터렛은 플레이어 거리와 무관하게 지도에서 켜기/끄기·강화를 허용하는 기존 UI/Host 정책을 유지한다. 미점유 원격 대상과 잠금/전력/냉각/입력 제약은 우회하지 않는다.
- Field 타이머 아래에 `구역 확보 중`/`확보 일시 중단`/`확보 구역 이탈`/`미점유 지역`을 아이콘과 함께 표시한다. 실제 진행을 보던 로컬 플레이어가 구역 밖으로 나간 때 이탈2.4초를 보여 주고 현재 상태로 복귀한다. 완료/취소된 과거 확보 구역은 이탈 대상으로 남기지 않는다. 텍스트는 상태 변경 때만0.5초/10% 확대 후 돌아오고 매 틱 반복하지 않는다. 진행/중단은 기존 전력색과 본문색, 이탈은 적색과 본문색을55% 혼합해 밝은 잔디와 붉은 지형에서도 읽히게 하고 기존 AlertText 외곽선을 유지한다.

## 편집/데이터 경계

TacticalHud.prefab의 ModeEffect 시간, Field region status anchor/폰트/심볼, TacticalModeFrame/TacticalTerritory Material을 편집한다. 기존 BodyFont·AlertText 외곽선·공통 패널을 재사용한다. 새 bitmap이나 글자 모양 이미지로 대체하지 않는다. 기존 Scene/Prefab GUID와 장식 renderer 참조를 유지한다.

상태/조작 권한은 확정 RegionGridSnapshot·Host presentation이다. 시각 feather는 점유 판정에 사용하지 않는다. 기존 Client 계약에 없는 정적 확보 진행은 추정하지 않는다. Field 바닥 띠의 별도 구현은 Core 세션이 맡으며 여기서는 지도와 동일한 공용 월드 외곽선 연결만 한다.

## 검증

- Tactical17개, Stage Scene9개(공통1개 중복), FieldTerritory5개, 점유 원격 권한1개의 고유31개 회귀를 확인했다. Scene의 기존8개와 새 FullMembrane1개는 분리 실행 결과를 합쳤다. 최초 종료 프레임 검사 실패는 EditMode fixture가 정상 Player 프레임을 기다리도록 수정한 뒤 재검사했다. 마지막 의미색 보정은 컴파일과 정상 Play 화면으로 별도 확인했다.
- 실제 OS 마우스로 지도 열기/닫기·상태 아이콘 hover를 확인했다. 정상 Player/렌더 프레임에서30 PNG를 기록했고, 전체 덮임→중앙 제거→가장자리 잔상과 조작 UI 공개 순서를 직접 확인했다. 전환 중 Command alpha>0인 표본은0개였다. Field 문구의 실제 scale은1→1.0985→1로 복귀했다.
- 초기 긴 run_script 안의 연속 촬영에서는 CommandRoot alpha1/활성 상태여도 지도 UI가 캡처되지 않았다. 이를 동작 완료나 결함으로 단정하지 않고, Game 탭이 보이는 상태의 실제 native 클릭과 정상 프레임 관측으로 다시 확인했다. 이때 지도 graphic/닫기/전체도 depth와 inherited alpha1/cullfalse, 실제 화면까지 확인했다. 제품 렌더링 우회 코드는 추가하지 않았다.
- Field 문구는 임시 Host 정적 진행 복원 fixture로 확보 진행(Holding)→중단(EnemyPressure/NeedsCombat)→이탈(NoParticipant)→미점유 복귀를 확인했다. UI 글자를 직접 바꾸지 않고 기존 RegionSceneBinding/StaticRegionCapture의 실제 표본을 사용했다. 자연 전투 피해 입력이나 원격 Client 진행 배포 검증을 뜻하지 않는다. 종료 시 설정·진행·플레이어 위치를 복구했다.
- Field/지도 공유 경계와 GPU 바닥색·오브젝트 가림·점유 revision 갱신은 [Core의 별도 검증 기록](2026-11-14-field-territory-boundary.md)을 따른다. 실제 native 열기/닫기 뒤 map-open-state/field-return-state를 각각 읽어 선분4/4·Match/Grid/revision11일치와 Field 표시 숨김/복귀를 확인했다.
- 전체 Assets .meta 검사0오류, 전술 Prefab1266개 문서의 fileID 중복0을 확인했다. 기존 스테이징8파일은 원본 binary patch와 동일하다. 이번 후속에서 커밋·푸시·PR을 수행하지 않았다. Standalone 빌드·물리 키보드·긴 held-drag는 이번 검증에 포함하지 않는다.

검수 자료는 ignored `Docs/Local/Validation/TacticalWave20261114`의 tests-*-final.json, native/samples.json과 PNG, field-status-final/result.json에 있다. 최종 색 검수는 동일한 중단 표시를 NeedsCombat 진행 fixture로 만들었고, 초기 EnemyPressure 검수와 구분한다. 기존 중간 실패는 최종 결과와 구분해 보존한다.
