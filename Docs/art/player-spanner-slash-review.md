# 스패너 초승달 검격

담당: 이영빈 · 2026-10-31.

스패너 공격의 실제 타격 순간에 흰색 중심과 옅은 청록색 테두리의 초승달 검격을 한 번 표시합니다. 빗나간 공격에도 표시하며 수리 동작과 취소된 공격에는 표시하지 않습니다. 공격 방향은 준비 동작을 시작할 때 저장한 방향을 사용합니다.

검격은 `Assets/Prefabs/Player/VisualsV2/SpannerSlash.prefab`에 SpriteRenderer·Animator·PlayerSpriteEffect·PlayerSpannerSlash로 배치했습니다. Inspector에서 그림, 애니메이션, 수명과 정렬을 편집할 수 있습니다. `PlayerSpanner.prefab`의 `_slashPrefab`으로 연결하며 런타임에 그림이나 메시를 생성하지 않습니다.

새 그림은 내장 `image_gen`으로 생성했습니다. 원본은 `Assets/Art/Sprites/Player/Motions/V2/Source/SpannerSlash_Source.png`, 실제 프레임 4장은 `Assets/Art/Sprites/Player/Motions/V2/SpannerSlash/`에 저장했습니다. 생성 요청과 해시는 [아트 기록](player-spanner-slash-review.json)에 포함합니다. 새 이펙트 애니메이션은 캐릭터 몸체 모션과 구분해 `Assets/Art/Animations/Player/Effects/`에 배치했습니다.

공유 Sprite 피벗은 256×256 캔버스의 (74,128), 128 PPU입니다. 피벗부터의 기준 반경 178px(1.390625 로컬 단위)을 실제 공격 `_range`에 맞춰 확대합니다. 가시 알파 32 이상인 그림은 현재 공격 각도 100도 안에 들어오도록 피벗을 등록했습니다. 장식 그림은 공격 경계 안의 일부를 표시하며, 피해 판정과 장애물 가림은 기존 스패너 코드가 계속 처리합니다. `_arc` 자체를 다른 값으로 바꾸면 해당 각도에 맞는 그림도 조정해야 합니다.

검격은 타격 당시 플레이어의 월드 중심·Z·정렬을 사용하며 이후 이동을 따라가지 않습니다. 0.18초 동안 4프레임과 알파 감소를 재생하고 제거됩니다. [8방향 조합 미리보기](player-spanner-slash-preview.gif)는 현재 캐릭터와 분리 무기를 실제 표시 배율로 합성한 검토 자료이며 Unity 실행 영상은 아닙니다.

컴파일과 에셋 참조·각도·반경·알파·수명 검사를 수행했습니다. 실제 PlayMode 테스트와 적 피해·수리·벽 근처에서의 최종 화면 확인은 실행하지 않았습니다.
