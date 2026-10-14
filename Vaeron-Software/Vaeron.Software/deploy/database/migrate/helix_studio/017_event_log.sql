-- 啟用、開啟、解鎖從 tool 的 use 列搬到 event_log（發生過的事，一件一列，只新增不改寫）
--   舊紀錄沒有記到是誰、哪一支方法處理，那兩欄留空

CREATE TABLE IF NOT EXISTS dbo.event_log (
    id            serial PRIMARY KEY,
    type          text NOT NULL,                       -- activate / open / unlock / error
    tool_no       text,                                -- 哪一支工具；不是對著工具發生的是空的
    account       text,                                -- 誰：當時登入 Windows 的帳號
    function_name text,                                -- 系統由哪一支方法處理
    message       text,                                -- 處理的結果；[error] 錯誤訊息
    stack_trace   text,                                -- [error] 呼叫堆疊
    created_at    timestamptz NOT NULL DEFAULT now(),
    updated_at    timestamptz NOT NULL DEFAULT now()
);

COMMENT ON TABLE dbo.event_log IS '發生過的事：每一次啟用、開啟、解鎖設定工具，每一個沒被接住的錯誤，一件一列，只新增不改寫，全部留著';
COMMENT ON COLUMN dbo.event_log.id IS '流水號';
COMMENT ON COLUMN dbo.event_log.type IS 'activate 啟用工具／open 開啟工具／unlock 解鎖工具設定／error 沒被接住的錯誤';
COMMENT ON COLUMN dbo.event_log.tool_no IS '哪一支工具，對到 tool.tool_no；不是對著工具發生的是空的';
COMMENT ON COLUMN dbo.event_log.account IS '誰：當時登入 Windows 的帳號；這張表建立前搬進來的紀錄沒有記到，是空的';
COMMENT ON COLUMN dbo.event_log.function_name IS '系統由哪一支方法處理，例如 MainWindow.Run；這張表建立前搬進來的紀錄沒有記到，是空的';
COMMENT ON COLUMN dbo.event_log.message IS '處理的結果；[error] 錯誤訊息';
COMMENT ON COLUMN dbo.event_log.stack_trace IS '[error] 呼叫堆疊';
COMMENT ON COLUMN dbo.event_log.created_at IS '發生的時刻';
COMMENT ON COLUMN dbo.event_log.updated_at IS '最後一次改動的時刻；紀錄不改寫，與發生的時刻相同';

INSERT INTO dbo.event_log (type, tool_no, created_at, updated_at)
SELECT action, tool_no, created_at, updated_at FROM dbo.tool WHERE type = 'use' ORDER BY created_at, id;

DELETE FROM dbo.tool WHERE type = 'use';
ALTER TABLE dbo.tool DROP COLUMN action;

COMMENT ON TABLE dbo.tool IS '工具：一列是什麼由 type 決定。tool 是匯入的一支工具（匯入就是裝好，移除就是整列拿掉）；key 是啟用時存下的工具金鑰，最新的那一把就是現在用的；啟用、開啟、解鎖記在 event_log';
COMMENT ON COLUMN dbo.tool.type IS 'tool 一支工具／key 一把工具金鑰';
COMMENT ON COLUMN dbo.tool.tool_no IS '工具代碼，套件 manifest 的 tool_no；key 用它對到那一支工具';
COMMENT ON COLUMN dbo.tool.created_at IS '寫入時刻：tool 是第一次匯入、key 是貼進來的時刻';
