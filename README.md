# TextAdvance

[TextAdvance](https://github.com/NightmareXIV/TextAdvance) 的台服移植版。跑任務加速小工具：自動確認接取／完成任務、跳過字幕與過場動畫、自動處理各種確認提示。

## 功能

- 自動跳過任務對話字幕
- 自動跳過過場動畫（僅限本來就能跳過的）
- 自動確認任務接取與完成
- 自動與附近的任務相關物件互動
- 自動挑選最有價值的任務獎勵
- 自動填寫並確認「委託」（Request）視窗
- 可設定按住即可暫時停用／啟用插件的按鍵
- 可依區域分別設定各項開關，或指定角色登入時自動啟用

## 整合

- **[Splatoon](https://github.com/ffxiv-tc-port/Splatoon)**：在地圖上標示附近的任務相關物件
- **[vnavmesh](https://github.com/ffxiv-tc-port/vnavmesh)**：`/at mtq` 自動導航到最近的任務物件；`/at mtf` 自動導航到地圖旗標
- 提供 IPC 讓 [Questionable](https://github.com/ffxiv-tc-port/Questionable) 等插件在自動跑任務時暫時接管各項開關

## 使用

- **每次登出會自動停用**，需再次輸入 `/at` 才會重新啟用（可在設定中關閉此行為，或設定特定角色登入時自動啟用）
- `/at`：切換啟用／停用；`/at e|enable`／`/at d|disable` 指定開或關
- `/at s|settings`：開啟設定
- `/at g`：切換任務目標指示標記
- `/at mtq`、`/at mtf` 見上方整合說明；`/at mtqstop` 取消所有待執行的移動

## 台服版的修改

- **自動使用重要物品（KI）**：任務目標要求對目標使用任務道具時自動使用（預設關，可依區域設定；Questionable 會自己用道具，不需要開）。
- **委託道具品質偏好**：自動填寫委託時可指定優先用 NQ（保留 HQ）或 HQ。
- 修正 `/at d` 不會停用插件（原本被除錯用的同名參數攔走）。
- **台服相容性修正**：任務獎勵的裝備箱挑選改用繁中職能字串、Splatoon 認得台服的「指定地點」、任務標記名稱帶格式時也認得出來；遊戲改版後過場自動跳過的特徵碼對不上時，插件照常載入，只停用過場自動跳過。
- **不打斷別的插件的導航**：自動互動只在路徑是自己送出的、或別的插件的路徑終點就在這個物件旁邊時才停下路徑；走向任務目標途中卸載或更新插件時，會停下自己送出的路徑。
- 多處防閃退修正（對關閉中的視窗不重按、跨插件 IPC 改在主執行緒執行），過場動畫中的對話不再只推進第一頁。
- 介面繁體中文化。

## 安裝

`/xlsettings` 開「Dalamud 設定」→「試驗性功能」→「第三方插件倉庫」，加入 `https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json` 並啟用、儲存，再到「插件管理員」（`/xlplugins`）安裝；同一個插件只裝這裡的版本，不要同時裝上游版。

原作者：[NightmareXIV](https://github.com/NightmareXIV/TextAdvance)

授權：上游未附授權。
