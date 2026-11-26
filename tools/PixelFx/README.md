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

내보내기는 [864×864 PNG](../../Assets/Resources/TreeLeafFall.png)를 갱신하고, 제작용 [프레임 JSON](TreeLeafFallFrames.json)은 이 폴더에 저장한다. Unity는 PNG를 Sprite (2D and UI) / Multiple로 가져온다. 96×96 고정 그리드의 처음 74칸을 왼쪽 위부터 행 순서대로 사용하며, 0–36번은 잎 4장, 37–73번은 잎 5장이다. 두 묶음 모두 15fps, 16 PPU, Custom Pivot (0.5, 0.125)을 사용한다. Point 필터, Mipmap 끔, 압축 없음, NPOT 크기 보존 설정은 PNG의 기존 meta에 있다.

`records/nature/tree-leaf-fall/four-contact.png`, `five-contact.png`로 프레임을 확인한다. Unity의 Animation 창에서 [4잎 클립](../../Assets/Art/Animations/World/Resources/TreeLeaves/TreeLeafFallFour.anim)과 [5잎 클립](../../Assets/Art/Animations/World/Resources/TreeLeaves/TreeLeafFallFive.anim)의 프레임 순서와 타이밍을 편집한다. [TreeLeafFallController](../../Assets/Art/Animations/World/Resources/TreeLeaves/TreeLeafFallController.controller)는 두 클립을 FourLeaves와 FiveLeaves 상태로 연결한다.

PixelFX와 잎 모양의 기반인 Sprite Fusion falling leaves에 대한 저작권·허가문은 [LICENSE.txt](LICENSE.txt)에 있다.
