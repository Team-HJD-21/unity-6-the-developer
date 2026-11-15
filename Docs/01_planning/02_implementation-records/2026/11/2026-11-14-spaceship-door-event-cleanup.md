# 우주선 문 애니메이션의 유실된 이벤트 호출 정리

main #722에서 SpaceshipAnimationEventAdapter가 제거됐지만 Spaceship_Stage_Door/Close 클립이 DeliveryNearPlayerFlag를 호출해 실제 Stage_1에서 ‘has no receiver’ 오류와 Error Pause가 발생했다.

두 클립의 유실된 이벤트1개씩만 제거했다. 키프레임·상태 전환·GUID와 현재 SpaceshipEnter의 충돌→Core 이동 API는 유지한다. 삭제된 PoC 수신기를 다시 만들거나 새 자동 부착 component를 넣지 않았다. 현재도 실행되지 못하던 호출을 걷어낸 것이며 다른 DoorOperation은 변경하지 않는다.

실제 Unity AnimationUtility에서 두 클립의 이벤트0을 확인했고, 새 Play에서 문 열기/닫기를 거친 구조 검수의 새 Runtime 오류0을 확인했다. 앞선 오류·stack은 Docs/Local/Execution/EncounterStage1-20261113/b6a-callback-errors.json, 검수 결과는 door-hotfix-actual-events.json 및 b6a-actual-final-errors.json에 보존했다. World/Spaceship 담당자에게 알릴 변경이며 source/Scene 전체 재작성은 없다.
