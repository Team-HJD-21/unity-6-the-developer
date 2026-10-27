# 냉각총·스나이퍼 외형 확장

작성: 이영빈 · 2026-10-27 · Issue #554 후속 작업.

기존 몸체와 손 자세를 사용하며, 별도 무기 그림과 전투 설정만 확장했다. 냉각총은 흰색 단열 외장과 청록색 냉각 링, 스나이퍼는 각진 개머리판·작은 조준경·가는 긴 총열로 구분한다.

- `Assets/Data/Player/Weapons/CryoBlasterAppearance.asset`: 냉각총의 몸체 프레임별 외형 92개.
- `Assets/Data/Player/Weapons/SniperAppearance.asset`: 스나이퍼의 몸체 프레임별 외형 92개.
- `Assets/Art/Sprites/Player/Motions/V2/SeparatedWeapons/CryoBlaster/`, `Sniper/`: 별도 Sprite. 32 PPU로 몸체와 크기를 맞춘다.
- `Player.prefab`: `PlayerWeaponVisual._sniper`와 `PlayerAttack._sniperBulletPrefab` 연결.
- `SniperBullet.prefab`: 탄속 70, 수명 1.2초. 피해 45와 간격 0.85초는 `PlayerAttack`의 Sniper 항목에서 편집한다.
- 1 스패너 / 2 기본총 / 3 냉각총 / 4 스나이퍼. Tab·휠 아래는 다음, 휠 위는 이전 슬롯. 공격: 기존 Space.

### 스나이퍼 탄환 연출

`SniperBullet.prefab`은 전용 `SniperProjectile.controller`와 `SniperFlight.anim`을 사용한다. 기존 Warm 탄환 그림은 그대로 재사용하며, `FX/Sniper/Sniper_0~3.png`의 Import 설정을 80 PPU로 지정해 기본 탄환(128 PPU)보다 1.6배 크게 표시한다. PNG 원본 픽셀은 같다. 탄속 70·수명 1.2초·피해 45·충돌 영역은 유지한다.

프리팹 자식 `Tracer Glow`에는 뒤쪽으로 늘린 반투명 SpriteRenderer를 미리 배치했다. 위치·크기·색·투명도는 해당 자식에서 편집한다. 이동 경로를 저장하는 TrailRenderer와 달리 탄환 뒤에 붙어 이동하며 탄환과 함께 사라진다. 별도 코드나 콜라이더는 추가하지 않았다.

명중 시에는 `VisualsV2/SniperImpact.prefab`을 사용한다. 기존 명중 Sprite 애니메이션을 1.6배로 표시하며 크기는 이 프리팹의 Transform에서 조절한다. 기본총·냉각총의 공용 탄환·명중 에셋은 변경하지 않았다. 연결과 물리 설정의 정적 검증은 완료했고 실제 플레이에서의 시인성·타격감 확인은 남아 있다.

총구는 외형 프레임의 `_overrideMuzzle`과 `_muzzleLocalPosition`에 플레이어 루트 기준으로 지정한다. `_shotMuzzleLocalPosition`은 사격 클립 시작 프레임의 총구이며 첫 발이 낮게 든 대기 자세의 총구에서 나오지 않게 한다. `PlayerWeaponRig`는 사격 시 이 좌표를 우선 사용한다. 기존 기본총·스패너와 완전히 가려진 위 이동 프레임은 기존 총구 선택을 유지한다.

제작은 내장 `image_gen`으로 수행했다. 원화는 `sources.zip`, 실제 사용한 프롬프트·해시·프레임별 좌표는 `variants.json`에 보관한다. 모델명과 비용은 도구가 제공하지 않아 기록하지 않았다. 원화의 총열이 격자 경계를 넘는 부분은 무기 전체의 소유 셀을 판별한 뒤 잘랐다. 기존 손 가림은 새 무기 Sprite의 투명 영역에 반영하며 런타임에 마스크나 그림을 생성하지 않는다.

`review.html`에서 같은 몸체에 기본총·냉각총·스나이퍼를 나란히 비교할 수 있다. `comparison-00.png`부터 `comparison-07.png`까지 전체 92개 자세의 정적 비교도 제공한다.

몸체 244개의 파일 해시, 기존 AnimationClip과 플레이어 물리 설정 보존을 검사한다. 위 이동 4개 프레임은 기존에도 무기가 완전히 가려져 투명 Sprite를 연결한다. 컴파일, 정적 연결 검사와 Unity Test Runner·실제 플레이 결과는 구분한다. 실행 결과는 `checks.json`에 기록하며 테스트 코드 작성만으로 플레이 통과를 주장하지 않는다.
