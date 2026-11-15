# 확정 점유 확장과 완료 HUD

관련 PR: [#734](https://github.com/Team-HJD-21/unity-6-the-developer/pull/734)

상태: UI 소스 구현·독립 관리형 검사 완료, 통합 Editor/Play·GPU·Client 검수 대기. Core 통합 담당자가 Editor와 Git 작업을 소유하며 이 UI 작업에서 실행하거나 저장하지 않았다.

## 실행 경로

- `TacticalCommandController.TerritoryFeedback`가 현재 `Source.Turrets.Regions`의 기존 `IRegionGridSource.Changed`를 구독한다. 배우/Match/Source 변경·비활성화·파괴 시 해제한다. 최초 구독은 현재 상태에 즉시 정착한다.
- `RegionGridSnapshot.Change.Kind`가 실제 capture 계열이고 `Change.Revision`이 새로 관찰된 경우에만 연출한다. 확보 뒤 해금으로 Snapshot.Revision이 증가해도 같은 Change.Revision은 다시 재생하지 않는다. 여러 revision을 합친 정상 Client 전달은 마지막 표시에서 최신 확정 영역까지 확장한다. gap 자체를 복원으로 추측하지 않는다. Baseline·Match/Grid/기하 세대 변경은 즉시 정착한다.
- `TacticalCommandController.Regions`는 구독한 최신 확정 Snapshot을 우선 반환하므로 상단 점유율·지도·조작 자격은 표시 전환을 기다리지 않는다. 기존 명령 권한/Host 판정은 바꾸지 않는다. 지도 영역 raster도 변화 시 갱신하며 새 지도 발광 Shader는 추가하지 않는다.
- `TerritoryExpansionState`는 이전 표시 거리장·최신 확정 거리장·월드 거리 기반 도달 시간을 계산한다. 확정 Snapshot은 불변이며 표시용 중간 상태를 게임 Grid에 발행하지 않는다. 기존/신규 경계 정점 수를 보간하지 않는다.
- `FieldTerritoryRenderer`와 기존 FieldTerritory Shader/재질이 표시를 그린다. 기존 표면의 메시·재질을 재사용하며 전환 중에만 같은 메시 안에 거리장용 quad를 포함한다. 끝에서는 공용 `RegionBoundaryGeometry`의 정확한 최신 띠로 정착하고 quad를 제거한다. 지형 위/객체 아래 Territory Sorting Layer와 Grid 월드 XY/OriginZ를 유지한다.

## 디자인·편집 접점

`TacticalHudStyle`의 CaptureSweepSeconds0.65 → CaptureGlowSeconds0.12 → CaptureSettleSeconds0.28을 사용한다. 5차 SmoothStep 파동이며 새 땅만 약하게 빛난다. CaptureGlow 기본값은 RGBA(.40,.76,.50,.16), 정착 후 기존 안쪽 초록/바깥 빨강은 유지한다. Loss0.16초, 짧은 Merge0.18초, 성공 알림 Fresh1초도 같은 Style에서 편집한다. Scene/Prefab/폰트 수정은 필요하지 않다.

연속 확보는 현재 표시 형태를 시작점으로 이어간다. 먼저 드러난 땅의 잔광은 재점멸시키지 않고 별도로 줄인다. 구멍·분리된 섬은 실제 신규 점유 마스크 안에서만 전파한다. 도달 시간은 CellWidth/CellHeight를 적용하며 거리장 생성은 두 번의 1차원 거리 변환과 신규 면 내부의 heap 탐색을 사용한다. 프레임마다 Texture를 재생성/업로드하지 않고 확정 변화·짧은 합치기 때만 갱신한다.

알림은 `StaticSecured`와 `SecuredRegionIds`만 근거로 짧게 합쳐 `점유 완료`를 송출한다. 기존 `GameAlerts.TryShowIfIdle` 3인자 API와 성공색·AlertStrip/폰트/배치·등퇴장 시간을 그대로 쓴다. 바닥 파동이 도착한 뒤 시도하고, 바쁘면 큐 뒤에 넣지 않으며 최대 Fresh 시간 내에만 재시도한다. 묶음의 절대 대기 상한은2.5초다. 상실·Baseline·우주선/사망·세대 교체에서는 대기를 제거한다. DynamicCapture와 Unlock을 셀별 축하로 바꾸지 않는다.

기존 `CombatHudAlertLayout`이 반원·툴팁·드래그·지도 도크를 피한다. 새 패널·Canvas·이미지·전역 Manager를 만들지 않았다. 지도/메뉴/전환으로 숨겨도 타임라인을 계속 소비하여 복귀 후 과거 확보를 다시 재생하지 않는다.

## 검증과 남은 인수

- Unity Editor/Play/Unity CLI를 호출하지 않았다. 대신 Unity 번들 Roslyn4.3.1·.NET6.0.21의 별도 프로세스에서 현재 Domain 소스와 실제 UnityEngine.CoreModule/NUnit, 새 순수 UI 모델과 동일 NUnit fixture를 컴파일·실행했다. `TerritoryExpansionTests` **17/17 통과**. 독립 관리형 검사이며 Unity Test Runner/전체 Assembly-CSharp/Shader 컴파일/실제 Scene 검수로 보고하지 않는다.
- 검사 범위: 확정 상태 즉시성·파동 순서, 확보→해금 중복, 연속 재목표/잔광, 짧은 병합, 먼 외곽 조기 노출 방지, 상실/동시 교체, 복원/늦은 접속, 복제된 동일 기하, 합쳐진 Client revision, 직사각형 거리장의 독립 brute-force 대조, 구멍/섬, 성공 사실/만료/재확보 key.
- `FieldTerritoryTests`에는 기존 Shader/Sorting 검증과 함께 파동 재질 속성 검사를 보강했다. 통합 담당자는 이 클래스와 `TerritoryExpansionTests`, 기존 Stage 지도/전환 검사를 Editor에서 실행하고 실제 30초 확보·Client·부드러운 파동/정착·기존 타이머/Alert 겹침을 직접 확인해야 한다. 확보·해금·스폰 제외는 Core 검수 대상이다.
- 아티팩트: Git 제외 `Docs/Local/Validation/TerritoryExpansion20261115`. managed harness 결과는 `results.json`, 컴파일 인자는 `compile.rsp`에 있다. 최초 네이티브 DLL까지 참조한 harness 설정 오류를 CLR 메타데이터가 있는 DLL만 선택하도록 고친 뒤 실제 fixture를 통과했다. Editor 오류로 해석하지 않는다.
- 폭/높이512셀 초과의 극단적인 Grid 또는 부동소수 Texture 미지원/half 범위 초과 환경은 거대/잘못된 Texture 대신 정확한 확정 경계에 즉시 정착한다. 판정과 실제 확보 완료 알림은 유지한다. 일반 Grid는 축당512 안에서 최대8 samples/cell을 사용한다.

거리장 Texture는 linear RGBAFloat(미지원 시 RGBAHalf)이고 signed 값과 잔광을 보존한다. Unity6의 지원 형식은 [Texture2D.SetPixels 공식 API](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture2D.SetPixels.html)를 확인했으며 업로드는 변화 때 한 번 수행한다. 실제 GPU 결과는 별도 검수 대기다.
