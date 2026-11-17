# 나무 잎사귀 PixelFX 제작

`tree-leaf-fall.js`는 PixelFX의 `nature/_lib.js`에 있는 잎 그림 함수를 사용한 짧은 낙하 연출이다. 4장·5장 버전을 하나의 투명 PNG와 Aseprite 형태 JSON으로 내보낸다.

기준 PixelFX 소스는 [patch-gg/pixelfx](https://github.com/patch-gg/pixelfx), 커밋 `5294518ce7c72e48b6100a744596e0acf57f2826`이다. Node 18 이상이 필요하다. PixelFX 제작 checkout은 Unity가 무시하는 `Library/PixelFxAuthoring`에 둔다.

저장소 루트에서 실행한다.

```powershell
git clone https://github.com/patch-gg/pixelfx.git Library/PixelFxAuthoring
git -C Library/PixelFxAuthoring checkout 5294518ce7c72e48b6100a744596e0acf57f2826
node tools/PixelFx/export-tree-leaves.mjs
node Library/PixelFxAuthoring/tools/check.mjs nature/tree-leaf-fall
```

이미 받아둔 다른 checkout을 쓰려면 내보내기 명령 끝에 경로를 전달한다. 내보내기는 그 checkout의 `src/fx/nature/tree-leaf-fall.js`에 이 폴더의 제작 소스를 복사한다.

산출물은 `Assets/Resources/TreeLeafFall.png`와 `TreeLeafFallFrames.json`이다. 864×864 투명 시트, 96×96 프레임 74개, `four`와 `five` 태그를 갖는다. Unity 가져오기 설정은 Point, Mipmap 끔, 압축 없음, NPOT 크기 보존이다. 기존 meta를 유지하면 가져오기 설정과 GUID가 보존된다.

`records/nature/tree-leaf-fall/four-contact.png`, `five-contact.png`로 프레임을 확인한다. 실제 게임은 `TreeLeafBurstFx`에서 재생한다.

PixelFX와 잎 모양의 기반인 Sprite Fusion falling leaves에 대한 저작권·허가문은 [LICENSE.txt](LICENSE.txt)에 있다.
