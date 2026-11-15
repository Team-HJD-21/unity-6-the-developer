# 우주선 충돌 회귀 수정

PR: 게시 후 번호 기입. 원인 기준 #731, 팀원 우주선 #732를 유지한 수정이다.

최근 Physics2D 조합 변경으로 Default–PlayerFeetBox, PocInteractArea–PlayerFeetBox 접촉이 차단되어 있었다. 우주선 벽/문은 Default였고 출구는 `SpaceshipExit.OnCollisionEnter2D`, 문 열림은 `DoorOperation.OnTriggerStay2D`에 의존한다. 이동 함수를 직접 부르는 검수만으로는 이 문제를 확인할 수 없다.

`WorldGeometry`(27)를 벽·닫힌 문의 전용 레이어로 사용한다. 지형 바닥용 Ground_1과 상호작용 감지용 PocInteractArea를 벽 용도로 재사용하지 않는다. WorldGeometry는 PlayerFeetBox·EnemyFeetBox·Bullet·Missile·EnemyProjectile·PlayerFieldDevice와만 접촉한다. 기존 Default/PocInteractArea와 PlayerFeetBox 접촉은 호환을 위해 복구하며 나머지 최적화 조합은 유지한다.

Scene에서는 Collider 소유 객체의 레이어와 필요한 Prefab instance override만 바꾼다. 팀원 지형 배치·문 로직·카메라를 되돌리지 않는다. 우선 확인할 파일은 Physics2DSettings, TagManager, SpaceShipGrayBox, SpaceshipCollisionRegressionTests다.

정적 검사4개 통과. 최신 #732 제작본에서 실제 Rigidbody2D 접촉 목록의 SideWall/Right·SideWall/Left, DoorOperation의 nearPlayer=true·닫힘 Collider 해제, SpaceshipExit의 물리 충돌에 따른 Stage_1 Match 생성·Player1개를 확인했다. 출격 함수를 직접 호출해 대체하지 않았다. 초기 일시정지 상태의 미실행 시험은 별도 보관하고 통과에 포함하지 않았다.

출구의 실제 목적지는 Stage_1이며 NetworkManager/Transport·방 배치·새 LeftRoom 원본을 되돌리지 않는다. LeftRoom 방 이동 전체 인수는 이번 출격 충돌 검수와 별개다.
