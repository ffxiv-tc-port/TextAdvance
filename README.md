# TextAdvance

跑任務加速小工具。自動確認接取／完成任務、跳過字幕與過場動畫、自動處理各種確認提示。

## 功能

- 自動跳過任務對話字幕
- 自動跳過過場動畫（僅限本來就能跳過的）
- 自動確認任務接取與完成
- 自動與附近的任務相關物件互動
- 自動挑選最有價值的任務獎勵
- 自動填寫並確認「委託」（Request）視窗
- 可設定按住即可暫時停用／啟用插件的按鍵

## 整合

- **Splatoon**：在地圖上標示附近的任務相關物件
- **vnavmesh**：`/at mtq` 自動導航到最近的任務物件；`/at mtf` 自動導航到地圖旗標

## 使用

- **每次登出會自動停用**，需再次輸入 `/at` 才會重新啟用（可在設定中關閉此行為，或設定特定角色登入時自動啟用）
- `/at enable`／`/at disable` 手動切換
- `/at mtq`、`/at mtf` 見上方整合說明

## 安裝

在 Dalamud 設定的「自訂插件庫」加入
`https://raw.githubusercontent.com/ffxiv-tc-port/DalamudPluginsTC/main/repo.json` 並啟用，
再從插件列表安裝。

## 作者與支援

原作 [NightmareXIV](https://github.com/NightmareXIV/TextAdvance)。
本分支為 [ffxiv-tc-port](https://github.com/ffxiv-tc-port) 針對台服官方繁中版維護的移植版。
