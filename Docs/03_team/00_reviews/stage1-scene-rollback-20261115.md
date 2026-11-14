# Stage_1 제작 변경 적용 보류

PR: [#727](https://github.com/Team-HJD-21/unity-6-the-developer/pull/727). 대상 main.

## 이유와 범위

팀원이 Stage_1을 대대적으로 제작 중이므로 사용자 요청에 따라 #726이 추가한 해당 Scene 변경 전체를 되돌린다. 기준은 #726의 main 첫 부모 `b529a02d3`이며, 현재 main에서 이 Scene에 후속 수정이 없음을 확인했다. 기존 팀원 배치·오브젝트·GUID를 보존한다.

- Tilemap 정렬 캐시 2곳, Core/지역/Encounter 설정 직렬화, 이전 출입구의 남은 필드 삭제, 전술 카메라/Scenery 참조를 원복한다.
- 되돌린 Scene의 Git blob이 #726 이전 `Assets/Scenes/Gameplay/Stage_1.unity`와 정확히 같음을 확인한다. 되돌리는 차이는 4줄 추가·49줄 삭제다.
- 런타임·Prefab·재질·TagManager는 이번 원복에서 변경하지 않는다. 코드 초기값은 그대로이므로 Scene 직렬화 제거가 기존 코드 기본값까지 되돌리는 의미는 아니다.
- 제작 참조의 적용은 미룬다. 카메라/Scenery의 명시 참조와 추가 출현 경고 Sprite 등은 팀원 Scene 제작 종료 후 확인해서 재적용한다. 보류 patch는 공유 제외 `Docs/Local/Review/Stage1SceneRollback-20261115/deferred-stage1-authoring.patch`에 보존한다.
- 진행 중인 Encounter 런처 분리·다른 UI 작업·기존 staging은 이 PR에 포함하지 않는다.

## 검증

Scene을 Unity에서 저장하지 않고 Git 역방향 patch를 적용했다. 원복 후 blob 동등성·diff/충돌 표시를 검사한다. Scene 이외 게임 코드 변경은 없으며, 이 긴급 원복에서 PlayMode 전체 인수를 새로 주장하지 않는다. 실제 CI 결과는 PR에 기록한다.
