# 무기 슬롯 아이콘 교체

작성: 이영빈 작업 지원 · 2026-10-27

1번 스패너와 2번 기본총을 손·캐릭터에 가려지지 않은 완전한 무기 형태로 교체했다. 기존 냉각총·스나이퍼와 어울리는 흑백·회색 외형을 사용한다.

## 런타임 연결

- `Assets/Art/UI/PlayerWeapons/Spanner.png`
- `Assets/Art/UI/PlayerWeapons/DefaultGun.png`
- Canvas: `Assets/Prefabs/UI/PlayerSkillHUD.prefab`

PNG는 128×64 투명 이미지이며 슬롯의 표시 영역은 92×46이다. 기존 `.meta` GUID와 프리팹 참조를 유지해 코드 변경 없이 연결된다. 3·4번 아이콘과 월드 무기·캐릭터 애니메이션은 수정하지 않았다.

## 생성 원본과 프롬프트

- 경로: 내장 `image_gen` (`art_source: host_image`).
- `sources.zip`: 선택한 생성 원본 2개. 원본의 투명도를 보존했다.
- `provenance.json`: 실제 프롬프트, 원본·최종 SHA-256, 크기, 알파 범위와 연결 검사 결과.
- 모델명과 비용은 도구가 반환하지 않아 기록하지 않았다.

이미지 처리는 알파 범위 자르기, premultiplied alpha LANCZOS 축소, 캔버스 중앙 배치만 사용했다. 무기 자체를 코드로 그리지 않았다.

## 확인

128×64와 실제 표시 크기를 밝은 배경·어두운 배경에서 확인했다. 두 PNG의 투명도·여백, 기존 GUID 연결, 냉각총·스나이퍼 PNG 유지 여부를 검사했다. Unity 플레이 화면에서의 확인은 별도다.
